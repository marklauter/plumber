# ServiceProvider concrete type exposed on public interface

- **Area:** IRequestHandler (API design)
- **Priority:** Medium
- **Status:** Open

## Problem
`IRequestHandler` exposes `ServiceProvider Services { get; }` as the concrete type rather than `IServiceProvider`. This couples consumers to Microsoft's DI implementation and exposes `Dispose()` on the root provider, inviting misuse.

## Suggested Fix
Change to `IServiceProvider Services { get; }`.

## Code References
- `Plumber/IRequestHandler{TRequest, TResponse}.cs:18` — `ServiceProvider Services { get; }`

## Notes
This is a breaking change to the public API.
