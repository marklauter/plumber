---
title: Plumber src review — four open findings
summary: Of eight code-review findings on src/Plumber, four are fixed; four remain open — late middleware construction, a Use-vs-invoke race, AmbiguousMatchException on overloaded InvokeAsync, and shared file-provider watchers across Build calls.
tags: [note, code-review, csharp, plumber]
created: 2026-06-12
document:
  status: open
---

# Plumber src review — four open findings

Code review of `src/Plumber` found eight issues; no injection-style vulnerabilities. Four are fixed (see below); four remain open and are detailed here. Original numbering is preserved so prior references hold. Findings cite symbols, not line numbers — the line numbers have drifted across the fix commits.

## Resolved

Findings 1, 2, 5, and 8a–8c are fixed (commits `f16762f`, `1f20504`, the `RequestContext` contract commit, and `834c983`): the `Production` environment default, async-aware disposal, the `RequestContext` single-threaded contract, the `InvokeAsync` null guards, the ctor provider-leak guard, and the `PlumberApplicationFactory` `Lazy`/identity fix. Details live in the commit messages.

## 3. Class middleware is constructed at first invoke, and the doc says otherwise

`Use<TMiddleware>()` registers a `next => new MiddlewareFactory<TMiddleware>(...)` lambda. That lambda runs inside `BuildPipeline()`, which the pipeline `Lazy` invokes on the first `InvokeAsync` — so the middleware instance is constructed at first invoke, not at registration.

Two consequences:

- **The doc is wrong.** The `Use<TMiddleware>()` remarks state the instance is "constructed once at registration time." It cannot be: the middleware's first constructor parameter is `RequestMiddleware next`, and `next` is a build-time artifact (each middleware's `next` is the already-built downstream, assembled in reverse by `BuildPipeline`). Construction is inherently deferred to pipeline build. The doc should say "constructed once when the pipeline is built, on the first `InvokeAsync`."
- **All shape validation fails late.** The `MiddlewareFactory` constructor does every structural check — `InvokeAsync` exists, returns `Task`, first parameter is the context type — plus `ActivatorUtilities.CreateInstance`. Because the factory runs at first invoke, a misconfigured registration (typo'd method name, wrong signature) throws from `InvokeAsync`, far from the `Use<T>()` call that caused it. The `NoInvokeAsyncMiddleware` / `WrongFirstParamMiddleware` / `WrongReturnTypeMiddleware` test fixtures all assert this late-failure behavior.

Suggestion: split `MiddlewareFactory` into eager shape-validation and deferred instantiation. The shape checks are static reflection over `typeof(TMiddleware)` — they need neither `next` nor the DI container, so run them synchronously inside `Use<TMiddleware>()` and fail at the call site. Keep `ActivatorUtilities.CreateInstance` + `Compile` in the deferred lambda, since those need `next`. Unresolvable constructor arguments still surface at build time (they need the instance), but the common typos fail fast. Pairs with finding 6 — do both in the same eager check. Then correct the doc.

## 4. Check-then-act race between Use and the first invoke

The private `Use` overload checks `handler.IsValueCreated`, then mutates `components` and `descriptors`. `BuildPipeline` reads `components` when the pipeline `Lazy` first resolves. A `Use` call racing the first `InvokeAsync` has three failure modes: the add lands after `BuildPipeline` snapshotted the list (middleware silently never runs), the add lands during enumeration (`InvalidOperationException`, collection modified), or `List` tears under concurrent mutation. `descriptors` is also read concurrently through the `Middleware` property's `AsReadOnly()`.

This looks like finding 8c (the factory's check-then-act, now fixed) but is not the same fix. The pipeline `Lazy` already guarantees `BuildPipeline` runs once; the contention is on the mutable `components` list feeding it, not on build-once-ness. A `Lazy` does not synchronize the list. In 8c, `Lazy` was the whole fix because the build decision *was* the contended state; here the build is already single, so `Lazy` buys nothing.

Severity: low. It requires concurrent `Use` and `InvokeAsync`, which is a usage error — the contract is "configure the pipeline, then invoke." But the silent-drop mode is the nastiest of the three when it does occur.

Suggestion: prefer the documentation route, matching how finding 5 (`RequestContext` threading) was resolved — state in the `Use` and `InvokeAsync` remarks, and as an architecture invariant, that pipeline configuration must happen-before the first invocation and is not thread-safe against it. If belt-and-suspenders is wanted, a `Lock` (the C# 13 `System.Threading.Lock`) around the `IsValueCreated`-check-plus-add in `Use`, taken again at the top of `BuildPipeline` (or snapshotting `components` to an array under it), closes all three windows in ~6 lines. Recommend documentation-first for consistency with finding 5, unless a real concurrent-configuration scenario exists.

## 6. Overloaded InvokeAsync throws AmbiguousMatchException

`MiddlewareFactory`'s constructor resolves the method with `type.GetMethod("InvokeAsync", BindingFlags.Instance | BindingFlags.Public)`. `Type.GetMethod(string, BindingFlags)` throws `AmbiguousMatchException` when the type declares more than one `InvokeAsync`. A middleware that overloads `InvokeAsync` (a common, innocent thing) crashes with a framework-internal exception carrying no Plumber context — and, per finding 3, it crashes at first invoke rather than at registration, compounding the confusion. This violates the fail-loud-with-named-context rule.

Suggestion: replace `GetMethod` with `GetMethods(BindingFlags.Instance | BindingFlags.Public)`, then filter to methods named `InvokeAsync` whose first parameter is the context type and whose return is `Task`-assignable. Zero matches reuse the existing "not found" / "first parameter" messages; two or more matches throw a new explicit message ("multiple `InvokeAsync` overloads accept `RequestContext` as the first parameter; the convention requires exactly one"). This both removes the crash and explains it. Fold it into finding 3's eager shape-validation, and add a `MultipleInvokeAsyncMiddleware` fixture following the existing `Wrong*Middleware` pattern.

## 7. Shared file-provider watchers leak across Build calls

`CreatePerBuildConfigurationBuilder` copies the shared builder's source *instances* into each per-build `ConfigurationBuilder` (`perBuild.Sources.Add(src)`). The reuse is deliberate for stateless sources, but file sources are not stateless: `FileConfigurationSource.EnsureDefaults` does `FileProvider ??= builder.GetFileProvider()` during `Build()`, so the first `Build()` caches a `PhysicalFileProvider` on the shared source instance, and every later `Build()` keeps it. With `reloadOnChange: true`, that provider owns a `FileSystemWatcher`.

Nothing disposes it. `ConfigurationRoot.Dispose()` disposes the `FileConfigurationProvider` instances it built (which release their change-token registrations) but not the `PhysicalFileProvider` — the provider doesn't own it, the source does, and the source outlives every handler. The watcher lives until process exit. A secondary effect: because the `FileProvider` is shared, a file change notifies every handler's `ConfigurationRoot` built from that source — cross-handler reload coupling.

Severity: low-to-moderate. The leak is bounded (one watcher per reload-enabled file source, created once — it does not grow per `Build()`), but it contradicts the documented promise that each `Build()` yields an independent handler whose configuration root is "disposed when the handler is disposed," and a long-lived process building many recipes with `reloadOnChange` accumulates watchers.

Suggestion: change the file-source `Add*` methods to enqueue deferred registration actions instead of materializing `IConfigurationSource` instances on the shared builder — the model `ConfigureConfiguration` callbacks already use. `AddJsonFile(path, optional, reloadOnChange)` would enqueue `cb => cb.AddJsonFile(path, optional, reloadOnChange)`, and `Build()` replays the queue against the fresh per-build builder, creating a fresh source (and fresh `PhysicalFileProvider`) per `Build()`. To fully close the leak the per-build file provider must also be owned and disposed with the handler — register it for disposal in the per-build container, or give each build its own base-path `PhysicalFileProvider` rather than sharing one through `Properties`. This is the most involved of the four and unifies the two configuration paths (direct `Add*` versus `ConfigureConfiguration`) into one deferred model. Lower-effort interim: document that `reloadOnChange` file sources shared across multiple `Build()` calls leak one watcher per source, and recommend one builder per long-lived reload-enabled handler.
