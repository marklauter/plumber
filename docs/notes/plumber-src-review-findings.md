---
title: Plumber src review — two open findings
summary: Of eight code-review findings on src/Plumber, six are fixed; two remain open — the Use-vs-first-invoke race in the core handler, and shared file-provider watchers leaking across Build calls.
tags: [note, code-review, csharp, plumber]
created: 2026-06-12
document:
  status: open
---

# Plumber src review — two open findings

Code review of `src/Plumber` found eight issues; no injection-style vulnerabilities. Six are fixed (see below); two remain open and are detailed here. Original numbering is preserved so prior references hold. Findings cite symbols, not line numbers — the line numbers have drifted across the fix commits.

## Resolved

- 1, 2, 5, 8a–8c — commits `f16762f`, `1f20504`, the `RequestContext` contract commit, and `834c983`: the `Production` environment default, async-aware disposal, the `RequestContext` single-threaded contract, the `InvokeAsync` null guards, the ctor provider-leak guard, and the `PlumberApplicationFactory` `Lazy`/identity fix.
- 3 and 6 — `MiddlewareFactory` now exposes a static `ValidateInvokeMethod` that `Use<TMiddleware>()` calls eagerly, so a misconfigured middleware (missing/duplicate `InvokeAsync`, wrong return type, wrong first parameter) fails at the registration call site, not on first `InvokeAsync`. This mirrors ASP.NET Core's `UseMiddleware`, which validates shape eagerly and defers only `ActivatorUtilities.CreateInstance`. The ambiguity check uses `GetMethods()` + a count `switch` instead of `GetMethod(name, flags)`, so overloaded `InvokeAsync` throws a named error rather than `AmbiguousMatchException` (finding 6). Instantiation stays deferred — it needs `next`, a build-time value. Doc corrected to say construction happens at pipeline build.

Details live in the commit messages.

## 4. Check-then-act race between Use and the first invoke

The private `Use` overload checks `handler.IsValueCreated`, then mutates `components` and `descriptors`. `BuildPipeline` reads `components` when the pipeline `Lazy` first resolves. A `Use` call racing the first `InvokeAsync` has three failure modes: the add lands after `BuildPipeline` snapshotted the list (middleware silently never runs), the add lands during enumeration (`InvalidOperationException`, collection modified), or `List` tears under concurrent mutation. `descriptors` is also read concurrently through the `Middleware` property's `AsReadOnly()`.

This looks like finding 8c (the factory's check-then-act, now fixed) but is not the same fix. The pipeline `Lazy` already guarantees `BuildPipeline` runs once; the contention is on the mutable `components` list feeding it, not on build-once-ness. A `Lazy` does not synchronize the list. In 8c, `Lazy` was the whole fix because the build decision *was* the contended state; here the build is already single, so `Lazy` buys nothing.

Severity: low. It requires concurrent `Use` and `InvokeAsync`, which is a usage error — the contract is "configure the pipeline, then invoke." But the silent-drop mode is the nastiest of the three when it does occur.

Suggestion: prefer the documentation route, matching how finding 5 (`RequestContext` threading) was resolved — state in the `Use` and `InvokeAsync` remarks, and as an architecture invariant, that pipeline configuration must happen-before the first invocation and is not thread-safe against it. If belt-and-suspenders is wanted, a `Lock` (the C# 13 `System.Threading.Lock`) around the `IsValueCreated`-check-plus-add in `Use`, taken again at the top of `BuildPipeline` (or snapshotting `components` to an array under it), closes all three windows in ~6 lines. Recommend documentation-first for consistency with finding 5, unless a real concurrent-configuration scenario exists.

## 7. Shared file-provider watchers leak across Build calls

`CreatePerBuildConfigurationBuilder` copies the shared builder's source *instances* into each per-build `ConfigurationBuilder` (`perBuild.Sources.Add(src)`). The reuse is deliberate for stateless sources, but file sources are not stateless: `FileConfigurationSource.EnsureDefaults` does `FileProvider ??= builder.GetFileProvider()` during `Build()`, so the first `Build()` caches a `PhysicalFileProvider` on the shared source instance, and every later `Build()` keeps it. With `reloadOnChange: true`, that provider owns a `FileSystemWatcher`.

Nothing disposes it. `ConfigurationRoot.Dispose()` disposes the `FileConfigurationProvider` instances it built (which release their change-token registrations) but not the `PhysicalFileProvider` — the provider doesn't own it, the source does, and the source outlives every handler. The watcher lives until process exit. A secondary effect: because the `FileProvider` is shared, a file change notifies every handler's `ConfigurationRoot` built from that source — cross-handler reload coupling.

Severity: low-to-moderate. The leak is bounded (one watcher per reload-enabled file source, created once — it does not grow per `Build()`), but it contradicts the documented promise that each `Build()` yields an independent handler whose configuration root is "disposed when the handler is disposed," and a long-lived process building many recipes with `reloadOnChange` accumulates watchers.

Suggestion: change the file-source `Add*` methods to enqueue deferred registration actions instead of materializing `IConfigurationSource` instances on the shared builder — the model `ConfigureConfiguration` callbacks already use. `AddJsonFile(path, optional, reloadOnChange)` would enqueue `cb => cb.AddJsonFile(path, optional, reloadOnChange)`, and `Build()` replays the queue against the fresh per-build builder, creating a fresh source (and fresh `PhysicalFileProvider`) per `Build()`. To fully close the leak the per-build file provider must also be owned and disposed with the handler — register it for disposal in the per-build container, or give each build its own base-path `PhysicalFileProvider` rather than sharing one through `Properties`. This is the most involved of the four and unifies the two configuration paths (direct `Add*` versus `ConfigureConfiguration`) into one deferred model. Lower-effort interim: document that `reloadOnChange` file sources shared across multiple `Build()` calls leak one watcher per source, and recommend one builder per long-lived reload-enabled handler.
