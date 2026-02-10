# Class-based middleware singleton captures root-scoped services

- **Area:** RequestHandler / MiddlewareFactory (middleware lifecycle)
- **Priority:** Medium
- **Status:** Open

## Problem
`Use<TMiddleware>()` instantiates middleware via `ActivatorUtilities.CreateInstance` at registration time using the root `ServiceProvider`. Constructor-injected scoped services (e.g., `DbContext`) will receive root-level instances that are shared across all requests, causing stale data, concurrency issues, or disposed context errors. Services injected into `InvokeAsync` parameters correctly use the scoped `context.Services`, but constructor injection does not.

## Suggested Fix
Document this behavior clearly. Consider per-request middleware instantiation or an `IMiddlewareFactory` pattern.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:143-146` — `ActivatorUtilities.CreateInstance` uses root `Services`
- `Plumber/RequestHandler{TRequest, TResponse}.cs:167-178` — `InvokeAsync` injection correctly uses scoped provider

## Notes
None.
