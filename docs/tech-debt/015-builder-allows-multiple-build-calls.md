# Builder allows Build() to be called multiple times

- **Area:** RequestHandlerBuilder (lifecycle)
- **Priority:** Medium
- **Status:** Resolved

## Problem
`Build()` can be called multiple times. Each call creates a new `RequestHandler` with an independent `ServiceProvider`, but they share the same `ConfigurationManager` instance (registered as `IConfiguration` singleton in each). Services added between builds create divergent handlers, and the shared configuration makes behavior unpredictable.

## Suggested Fix
Throw `InvalidOperationException` on the second call to `Build()`.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:39-50` — `Build()` with no guard against multiple calls

## Notes
None.

## Resolution
Reframed as the intended "recipe pattern" rather than a defect. Each `Build()` call now produces a fully independent `RequestHandler` with its own service provider and `IConfigurationRoot`. Internally, `Build()` shallow-copies the builder's `Sources` and `Properties` into a per-call `ConfigurationBuilder` before applying `ConfigureConfiguration` callbacks and `AddCommandLine`, so repeated calls don't duplicate sources or accumulate file watchers. Documented on the `Build()` overloads.
