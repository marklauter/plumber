# Elapsed uses DateTime.UtcNow instead of Stopwatch

- **Area:** RequestContext (timing)
- **Priority:** Low
- **Status:** Open

## Problem
`Elapsed` computes `DateTime.UtcNow - Timestamp`. `DateTime.UtcNow` has ~15.6ms resolution on Windows and is susceptible to clock adjustments. For performance metrics, `Stopwatch.GetElapsedTime()` (available in .NET 7+) provides high-resolution timing.

## Suggested Fix
Use `Stopwatch.GetElapsedTime()` or `Stopwatch.GetTimestamp()` for high-resolution elapsed time measurement.

## Code References
- `Plumber/RequestContext{TRequest, TResponse}.cs:50` — `DateTime.UtcNow - Timestamp`

## Notes
None.
