# Class-based middleware singleton captures root-scoped services

- **Area:** RequestHandler / MiddlewareFactory (middleware lifecycle)
- **Priority:** Medium
- **Status:** Resolved — working as designed; documented

## Resolution
This is the intended design, mirroring ASP.NET Core's convention-based middleware: a class middleware instance has effectively a singleton lifetime, and scoped/transient services are consumed via `InvokeAsync` parameter injection (resolved per-request from `context.Services`). The README covers this in *Middleware Method Injection* and the FAQ entry on middleware lifetimes.

The discoverability gap — a developer ctor-injecting a `DbContext` and silently getting singleton behavior — is addressed by `<remarks>` on both `Use<TMiddleware>` overloads in `Plumber/RequestHandler{TRequest, TResponse}.cs`, which spell out the singleton lifetime, the root-provider resolution of ctor params, and the InvokeAsync-parameter escape hatch.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs` — `Use<TMiddleware>` overloads carry the lifetime contract in XML docs
- `Plumber/RequestHandler{TRequest, TResponse}.cs` — `InvokeAsync` parameter injection resolves from the per-request scope (`context.Services`)

## Notes
A future enhancement could add a registration-time guard that throws when a ctor parameter type is registered as `Scoped` in the DI container, converting the footgun into a loud error. Not pursued here because it would require reflecting over the ctor and probing `IServiceProviderIsService`/descriptor lifetimes at `Use<T>()` time.
