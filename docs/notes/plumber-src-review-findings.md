---
title: Plumber src review — all findings resolved
summary: All eight code-review findings on src/Plumber are resolved — seven fixed, one (the Use-vs-invoke race) downgraded to not-a-defect.
tags: [note, code-review, csharp, plumber]
created: 2026-06-12
document:
  status: resolved
---

# Plumber src review — all findings resolved

Code review of `src/Plumber` found eight issues; no injection-style vulnerabilities. All are resolved — seven fixed, one downgraded to not-a-defect. Original numbering is preserved so prior references hold. Findings cite symbols, not line numbers — the line numbers have drifted across the fix commits.

## Resolved

- 1, 2, 5, 8a–8c — commits `f16762f`, `1f20504`, the `RequestContext` contract commit, and `834c983`: the `Production` environment default, async-aware disposal, the `RequestContext` single-threaded contract, the `InvokeAsync` null guards, the ctor provider-leak guard, and the `PlumberApplicationFactory` `Lazy`/identity fix.
- 3 and 6 — `MiddlewareFactory` now exposes a static `ValidateInvokeMethod` that `Use<TMiddleware>()` calls eagerly, so a misconfigured middleware (missing/duplicate `InvokeAsync`, wrong return type, wrong first parameter) fails at the registration call site, not on first `InvokeAsync`. This mirrors ASP.NET Core's `UseMiddleware`, which validates shape eagerly and defers only `ActivatorUtilities.CreateInstance`. The ambiguity check uses `GetMethods()` + a count `switch` instead of `GetMethod(name, flags)`, so overloaded `InvokeAsync` throws a named error rather than `AmbiguousMatchException` (finding 6). Instantiation stays deferred — it needs `next`, a build-time value. Doc corrected to say construction happens at pipeline build.
- 7 — resolved by removing the feature: Plumber no longer supports `reloadOnChange`. The watcher leak was the symptom; the disease was that a config reload reloads `IConfiguration` in place while the pipeline, provider, and bound options stay built-once — split-brain — and live reload is moot for Plumber's deployment reality (Lambda: frozen/read-only FS; CLI: exits; containers: immutable, config-in-env). Owners who need live config bring their own change trigger and rebuild a fresh handler from the recipe, swapping it (rebuild = re-read) — documented as a recipe. The `reloadOnChange` API surface is removed (3-arg `AddJsonFile`, the `AddUserSecrets` reload overload, the `AddDefaultConfigurationSources` reload param). With no watcher, repeated `Build()` is honestly independent as-is — no deferred-source refactor needed. The rejected alternatives (internal reload-with-generations; a `RequestHandler.OnConfigurationReloaded` callback that keeps `reloadOnChange` as a trigger) were both judged disproportionate to a use case Plumber's deployments rarely hit.

- 4 — not a defect. The original write-up framed `Use` mutating `components` while the first `InvokeAsync` builds the pipeline as a latent race. It isn't one in any correct program: the contract is configure-then-invoke. `Use` runs on the handler after `Build()` and before the first invoke; calling `Use` *after* the first invoke is already guarded (it throws `InvalidOperationException`); and concurrent *invocation* is supported and safe (the pipeline `Lazy` builds once — see `TwoInFlightInvocationsCompleteWithoutDeadlockAsync`). The only unguarded interleaving is calling `Use` on one thread while the first `InvokeAsync` runs on another — i.e., configuring a handler concurrently with invoking it, which no correct code does. That's instance-not-thread-safe-against-concurrent-mutation, the same documented norm as `DbConnection`, `HttpContext`, or `List<T>` — not a bug. No code change; "finish configuring before you invoke" is the contract.

Details live in the commit messages.
