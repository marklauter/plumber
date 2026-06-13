---
title: Plumber src review — eight findings closed
summary: A code review of src/Plumber surfaced eight findings; all are now resolved — seven fixed in code, one downgraded to not-a-defect. This entry preserves the record as the findings note is removed.
tags: [journal, code-review, csharp, plumber, milestone]
created: 2026-06-13
---

# Plumber src review — eight findings closed

A code review of `src/Plumber` (and `Plumber.Testing`) found eight issues, no injection-style vulnerabilities. All are resolved. This records what each was and how it was closed, since the working `plumber-src-review-findings.md` note is being deleted now that the backlog is empty.

## Resolutions

1. `AddDefaultConfigurationSources` defaulted the environment to `Development` when `DOTNET_ENVIRONMENT` was unset, inverting the .NET host convention. Fixed: defaults to `Production` (commit `f16762f`).
2. Per-request DI scope disposed synchronously, throwing for `IAsyncDisposable`-only services. Fixed: `CreateAsyncScope`, and `RequestHandler`/`PlumberApplicationFactory` implement `IAsyncDisposable` (commit `1f20504`).
3. Class-middleware shape validation fired late, at first `InvokeAsync`, not at registration. Fixed: a static `MiddlewareFactory.ValidateInvokeMethod` that `Use<TMiddleware>()` calls eagerly — mirrors ASP.NET Core's `UseMiddleware` (shape eager, `ActivatorUtilities.CreateInstance` deferred, since it needs `next`). Commit `e288f83`.
4. Framed as a `Use`-vs-first-invoke race. Downgraded to **not a defect**: configure-then-invoke is the contract, `Use`-after-invoke already throws, and concurrent invocation is safe. The only unguarded interleaving is configuring a handler on one thread while invoking on another — instance-not-thread-safe-against-concurrent-mutation, the norm for `DbConnection`/`HttpContext`/`List<T>`. No code change.
5. `RequestContext.Data` lazy init was unsynchronized. Resolved as a documented contract: `RequestContext` is single-threaded per request (pipeline invokes middleware sequentially), matching `HttpContext`. `ConcurrentDictionary` rejected. Commit `a199aa5`.
6. Overloaded `InvokeAsync` on a middleware threw `AmbiguousMatchException`. Fixed in the same change as finding 3 — `GetMethods()` + a count `switch` instead of `GetMethod(name, flags)` yields a named error.
7. A `FileSystemWatcher` leaked across `Build()` calls when `reloadOnChange: true`. Resolved by removing the feature — see decisions below. Merged in PR #4 (`69cc24f`).
8. Smaller items: `InvokeAsync` lacked a null guard (8a, `is null` form to avoid boxing value-type `TRequest`); the owning ctor leaked the provider when `TimeProvider` resolution threw (8b); `PlumberApplicationFactory.CreateHandler` was check-then-act and assumed `configurePipeline` returned the same handler (8c — became a `Lazy<RequestHandler>` with a `ReferenceEquals` identity check). Commit `834c983`.

## Decisions

Finding 7 drove the longest deliberation. The watcher leak was the symptom; the disease was that `reloadOnChange` reloads `IConfiguration` in place while the pipeline, provider, and bound options stay built-once — split-brain — and live file reload is moot for Plumber's deployment reality (Lambda frozen/read-only, CLI exits, containers immutable). Rejected internal reload-with-generations (atomic swap + refcount draining — disproportionate) and a `RequestHandler.OnConfigurationReloaded` callback (kept the watcher and its disposal tangle). Chose to remove `reloadOnChange` entirely and document reload as an owner-driven rebuild-and-swap: a fresh `Build()` re-reads config, the owner brings their own change trigger and swaps the handler at a quiescent point. With no watcher, repeated `Build()` is honestly independent as-is, so no deferred-source refactor was needed. The pattern ships as the Configuration-reload wiki recipe (`Recipe-Configuration-Reload`, with a compilable sample).

Finding 4's downgrade is the other judgment call: a "race" only reachable by violating the configure-then-invoke contract is not a library defect.

## Outcome

Eight of eight closed. `RequestHandlerBuilder` lost the `reloadOnChange` surface (breaking, pre-v4, migration-noted). One open question deferred to no one — the backlog is empty.
