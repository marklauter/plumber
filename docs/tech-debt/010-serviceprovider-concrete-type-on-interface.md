# ServiceProvider concrete type exposed on public interface

- **Area:** IRequestHandler (API design)
- **Priority:** Medium
- **Status:** Resolved

## Problem
`IRequestHandler` exposes `ServiceProvider Services { get; }` as the concrete type rather than `IServiceProvider`. This couples consumers to Microsoft's DI implementation and exposes `Dispose()` on the root provider, inviting misuse.

## Suggested Fix
Change to `IServiceProvider Services { get; }`.

## Code References
- `Plumber/IRequestHandler{TRequest, TResponse}.cs:18` — `ServiceProvider Services { get; }`

## Notes
This is a breaking change to the public API.

## Resolution
Resolved alongside removal of `IRequestHandler`/`IRequestHandlerBuilder`. `RequestHandler<TRequest, TResponse>.Services` is now typed `IServiceProvider` over a private `ServiceProvider` field; `Dispose()` is hidden from the public surface.
