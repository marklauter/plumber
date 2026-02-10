# Race condition in lazy pipeline build

- **Area:** RequestHandler (core pipeline)
- **Priority:** High
- **Status:** Open

## Problem
The `EnsureHandler()` method uses `handler ??= BuildPipeline()` which is not thread-safe. If two threads call `InvokeAsync` concurrently before the pipeline has been built, both may see `handler` as null and both will call `BuildPipeline()`. The `Use()` guard (`if (handler is not null)`) is also not synchronized — a thread could be in `BuildPipeline()` while another thread calls `Use()` and sees `handler` as still null. While Lambda serializes invocations per instance, this library targets general-purpose scenarios (console apps, message queue processors) where concurrent `InvokeAsync` calls are legitimate.

## Suggested Fix
Use `Lazy<T>` or `Interlocked.CompareExchange` to ensure the pipeline is built exactly once.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:92` — `EnsureHandler()` with non-thread-safe null-coalescing assignment
- `Plumber/RequestHandler{TRequest, TResponse}.cs:36-44` — `Use()` checks `handler is not null` without synchronization
- `Plumber/RequestHandler{TRequest, TResponse}.cs:77-90` — `InvokeInternalAsync` calls `EnsureHandler()` on every invocation

## Notes
None.
