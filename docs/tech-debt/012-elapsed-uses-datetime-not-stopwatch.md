# Elapsed uses DateTime.UtcNow instead of Stopwatch

- **Area:** RequestContext (timing)
- **Priority:** Low
- **Status:** Resolved

## Problem
`Elapsed` computes `DateTime.UtcNow - Timestamp`. `DateTime.UtcNow` has ~15.6ms resolution on Windows and is susceptible to clock adjustments. For performance metrics, `Stopwatch.GetElapsedTime()` (available in .NET 7+) provides high-resolution timing.

## Suggested Fix
Use `Stopwatch.GetElapsedTime()` or `Stopwatch.GetTimestamp()` for high-resolution elapsed time measurement.

## Code References
- `Plumber/RequestContext{TRequest, TResponse}.cs` — `Elapsed` now delegates to `TimeProvider.GetElapsedTime(startTimestamp)`

## Resolution
`RequestContext` now takes a `TimeProvider` instead of a `DateTime` timestamp. The ctor captures both a wall-clock `Timestamp` (`GetUtcNow().UtcDateTime`) for logging/correlation and a Stopwatch tick (`GetTimestamp()`) for high-resolution monotonic elapsed measurement. `RequestHandlerBuilder.Build` registers `TimeProvider.System` via `TryAddSingleton` so user-supplied providers (e.g., `FakeTimeProvider`) win.

## Notes
None.
