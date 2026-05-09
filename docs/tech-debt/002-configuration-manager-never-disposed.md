# ConfigurationManager is never disposed

- **Area:** RequestHandlerBuilder (configuration lifecycle)
- **Priority:** Medium
- **Status:** Resolved

## Problem
`ConfigurationManager` is created and registered as `IConfiguration` singleton. The DI container only disposes singletons it *created* itself — externally provided instances are not tracked for disposal. The file watchers from `reloadOnChange: true` will never be disposed, leaking file handles.

## Suggested Fix
Register the `ConfigurationManager` as its concrete type so the DI container tracks it, or disable `reloadOnChange` (sensible for Lambda/console apps), or have `RequestHandler` explicitly dispose it.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:15-16` — ConfigurationManager created
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:25` — `reloadOnChange: true`
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:45` — Registered as `IConfiguration`

## Notes
None.

## Resolution
`RequestHandlerBuilder<TRequest, TResponse>` passes its `ConfigurationManager` to `RequestHandler<TRequest, TResponse>` at `Build()` time. `RequestHandler.Dispose()` disposes both the service provider and the owned `ConfigurationManager`, releasing the file watchers from `reloadOnChange: true`.
