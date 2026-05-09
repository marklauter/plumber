# Race condition in lazy pipeline build

- **Area:** RequestHandler (core pipeline)
- **Priority:** High
- **Status:** Resolved

## Problem
The `EnsureHandler()` method uses `handler ??= BuildPipeline()` which is not thread-safe. If two threads call `InvokeAsync` concurrently before the pipeline has been built, both may see `handler` as null and both will call `BuildPipeline()`. The `Use()` guard (`if (handler is not null)`) is also not synchronized — a thread could be in `BuildPipeline()` while another thread calls `Use()` and sees `handler` as still null. While Lambda serializes invocations per instance, this library targets general-purpose scenarios (console apps, message queue processors) where concurrent `InvokeAsync` calls are legitimate.

## Suggested Fix
Use `Lazy<T>` or `Interlocked.CompareExchange` to ensure the pipeline is built exactly once.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs` — `handler` field, ctor, `Use()`, `InvokeInternalAsync`

## Notes
Scope clarified during triage: only the *concurrent `InvokeAsync`* race needs covering. `Use()` is a configuration-phase API, called single-threaded before the first invocation (same contract as ASP.NET Core's `IApplicationBuilder`). The `if (handler.IsValueCreated) throw` guard in `Use()` is a developer-mistake fail-fast, not a thread-safety contract.

## Resolution
`handler` changed from a nullable field with `??=` initialization to a `Lazy<RequestMiddleware<TRequest, TResponse>>` constructed in the ctor with `BuildPipeline` as the factory. Default `LazyThreadSafetyMode.ExecutionAndPublication` guarantees:
- `BuildPipeline` runs at most once across concurrent `InvokeAsync` calls.
- The published pipeline is safely visible without ad-hoc fences (handles weak-memory-model targets like ARM64).
- Exceptions from `BuildPipeline` are cached and rethrown on subsequent reads.

`InvokeInternalAsync` now calls `handler.Value` directly. `EnsureHandler()` was deleted (single call site, inlined). `Use()`'s guard switched to `handler.IsValueCreated`.

No regression test was added: `Lazy<T>` is BCL — testing it is testing what we don't own. Existing happy-path tests cover the wiring.
