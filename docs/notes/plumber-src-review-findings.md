---
title: Plumber src review — one open finding
summary: Of eight code-review findings on src/Plumber, seven are fixed; one remains open — the Use-vs-first-invoke race in the core handler.
tags: [note, code-review, csharp, plumber]
created: 2026-06-12
document:
  status: open
---

# Plumber src review — one open finding

Code review of `src/Plumber` found eight issues; no injection-style vulnerabilities. Seven are fixed (see below); one remains open and is detailed here. Original numbering is preserved so prior references hold. Findings cite symbols, not line numbers — the line numbers have drifted across the fix commits.

## Resolved

- 1, 2, 5, 8a–8c — commits `f16762f`, `1f20504`, the `RequestContext` contract commit, and `834c983`: the `Production` environment default, async-aware disposal, the `RequestContext` single-threaded contract, the `InvokeAsync` null guards, the ctor provider-leak guard, and the `PlumberApplicationFactory` `Lazy`/identity fix.
- 3 and 6 — `MiddlewareFactory` now exposes a static `ValidateInvokeMethod` that `Use<TMiddleware>()` calls eagerly, so a misconfigured middleware (missing/duplicate `InvokeAsync`, wrong return type, wrong first parameter) fails at the registration call site, not on first `InvokeAsync`. This mirrors ASP.NET Core's `UseMiddleware`, which validates shape eagerly and defers only `ActivatorUtilities.CreateInstance`. The ambiguity check uses `GetMethods()` + a count `switch` instead of `GetMethod(name, flags)`, so overloaded `InvokeAsync` throws a named error rather than `AmbiguousMatchException` (finding 6). Instantiation stays deferred — it needs `next`, a build-time value. Doc corrected to say construction happens at pipeline build.
- 7 — resolved by removing the feature: Plumber no longer supports `reloadOnChange`. The watcher leak was the symptom; the disease was that a config reload reloads `IConfiguration` in place while the pipeline, provider, and bound options stay built-once — split-brain — and live reload is moot for Plumber's deployment reality (Lambda: frozen/read-only FS; CLI: exits; containers: immutable, config-in-env). Owners who need live config bring their own change trigger and rebuild a fresh handler from the recipe, swapping it (rebuild = re-read) — documented as a recipe. The `reloadOnChange` API surface is removed (3-arg `AddJsonFile`, the `AddUserSecrets` reload overload, the `AddDefaultConfigurationSources` reload param). With no watcher, repeated `Build()` is honestly independent as-is — no deferred-source refactor needed. The rejected alternatives (internal reload-with-generations; a `RequestHandler.OnConfigurationReloaded` callback that keeps `reloadOnChange` as a trigger) were both judged disproportionate to a use case Plumber's deployments rarely hit.

Details live in the commit messages.

## 4. Check-then-act race between Use and the first invoke

The private `Use` overload checks `handler.IsValueCreated`, then mutates `components` and `descriptors`. `BuildPipeline` reads `components` when the pipeline `Lazy` first resolves. A `Use` call racing the first `InvokeAsync` has three failure modes: the add lands after `BuildPipeline` snapshotted the list (middleware silently never runs), the add lands during enumeration (`InvalidOperationException`, collection modified), or `List` tears under concurrent mutation. `descriptors` is also read concurrently through the `Middleware` property's `AsReadOnly()`.

This looks like finding 8c (the factory's check-then-act, now fixed) but is not the same fix. The pipeline `Lazy` already guarantees `BuildPipeline` runs once; the contention is on the mutable `components` list feeding it, not on build-once-ness. A `Lazy` does not synchronize the list. In 8c, `Lazy` was the whole fix because the build decision *was* the contended state; here the build is already single, so `Lazy` buys nothing.

Severity: low. It requires concurrent `Use` and `InvokeAsync`, which is a usage error — the contract is "configure the pipeline, then invoke." But the silent-drop mode is the nastiest of the three when it does occur.

Suggestion: prefer the documentation route, matching how finding 5 (`RequestContext` threading) was resolved — state in the `Use` and `InvokeAsync` remarks, and as an architecture invariant, that pipeline configuration must happen-before the first invocation and is not thread-safe against it. If belt-and-suspenders is wanted, a `Lock` (the C# 13 `System.Threading.Lock`) around the `IsValueCreated`-check-plus-add in `Use`, taken again at the top of `BuildPipeline` (or snapshotting `components` to an array under it), closes all three windows in ~6 lines. Recommend documentation-first for consistency with finding 5, unless a real concurrent-configuration scenario exists.
