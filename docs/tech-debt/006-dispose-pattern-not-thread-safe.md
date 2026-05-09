# Dispose pattern is not thread-safe

- **Area:** RequestHandler (lifecycle)
- **Priority:** Medium
- **Status:** Resolved — not a bug (2026-05-09)

## Problem
The `Dispose()` method and `ThrowIfDisposed()` read/write the `disposed` field without synchronization. Two concurrent `Dispose()` calls can both call `Services.Dispose()`. A concurrent `InvokeAsync` can pass `ThrowIfDisposed()` then have the service provider disposed underneath it.

## Suggested Fix
Use `Interlocked.Exchange` for the disposed flag.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:18` — `private bool disposed;`
- `Plumber/RequestHandler{TRequest, TResponse}.cs:181-194` — `Dispose()` and `ThrowIfDisposed()`

## Resolution
Closed as "not a bug" on 2026-05-09. Reasoning:

- Bool reads/writes are atomic in .NET, so the `disposed` field itself doesn't tear.
- `IDisposable.Dispose` is not contractually thread-safe — the standard `if (disposed) return; ... disposed = true;` guard is the Microsoft-recommended pattern, used throughout the BCL and the broader ecosystem.
- The underlying disposables this handler owns (`ServiceProvider`, `ConfigurationManager`) tolerate double-`Dispose`, so even a hypothetical race into `Dispose` is benign.
- Realistic usage: `InvokeAsync` is the concurrent hot path (called from many threads — see #001's `Lazy<>` fix), but `Dispose` is a one-shot shutdown event called after invocations have stopped. There is no realistic scenario where two threads race into `Dispose`, or where `ThrowIfDisposed` runs concurrently with `Dispose`.

## Notes
None.
