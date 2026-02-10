# Builder allows Build() to be called multiple times

- **Area:** RequestHandlerBuilder (lifecycle)
- **Priority:** Medium
- **Status:** Open

## Problem
`Build()` can be called multiple times. Each call creates a new `RequestHandler` with an independent `ServiceProvider`, but they share the same `ConfigurationManager` instance (registered as `IConfiguration` singleton in each). Services added between builds create divergent handlers, and the shared configuration makes behavior unpredictable.

## Suggested Fix
Throw `InvalidOperationException` on the second call to `Build()`.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:39-50` — `Build()` with no guard against multiple calls

## Notes
None.
