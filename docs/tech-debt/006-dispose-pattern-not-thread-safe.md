# Dispose pattern is not thread-safe

- **Area:** RequestHandler (lifecycle)
- **Priority:** Medium
- **Status:** Open

## Problem
The `Dispose()` method and `ThrowIfDisposed()` read/write the `disposed` field without synchronization. Two concurrent `Dispose()` calls can both call `Services.Dispose()`. A concurrent `InvokeAsync` can pass `ThrowIfDisposed()` then have the service provider disposed underneath it.

## Suggested Fix
Use `Interlocked.Exchange` for the disposed flag.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:18` — `private bool disposed;`
- `Plumber/RequestHandler{TRequest, TResponse}.cs:181-194` — `Dispose()` and `ThrowIfDisposed()`

## Notes
None.
