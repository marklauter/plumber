# Plumber.Serilog.Extensions needs to be updated for the callback-based builder

- **Area:** External package (`marklauter/plumber.serilog.extensions`)
- **Priority:** High
- **Status:** Open

## Problem
The Plumber.Serilog.Extensions package targets the old builder shape, where `RequestHandlerBuilder<,>` exposed `Services` and `Configuration` properties directly. Its sample and tests register Serilog via:

```csharp
handlerBuilder.Services
    .AddSerilog()
    .AddLogging(loggingBuilder => loggingBuilder.AddSerilog());
```

After Plumber's refactor to callback-based registration (`ConfigureServices` / `ConfigureLogging`), `handlerBuilder.Services` no longer exists. The package will not compile against the new Plumber and any consumer attempting the documented pattern will fail.

Additionally, the new builder's `ConfigureLogging` is guarded — `services.AddLogging(...)` only runs when at least one logging callback is registered. A consumer that only does `ConfigureServices((_, s) => s.AddSerilog())` (without a matching `ConfigureLogging`) gets `InvalidOperationException` when middleware resolves `ILogger<T>`.

## Suggested Fix
Update `Plumber.Serilog.Extensions` to:

1. Replace direct `builder.Services` access in samples and tests with the callback API:
   ```csharp
   var handler = RequestHandlerBuilder.Create<TReq, TRes>(args)
       .ConfigureServices((_, s) => s.AddSerilog())
       .ConfigureLogging((_, b) => b.AddSerilog())
       .Build();
   ```
2. Update README and `instructions-for-llms.md` to document the `ConfigureLogging` requirement explicitly.
3. Bump the Plumber dependency to the new major version once it's published.
4. Bump the package's own major version (breaking API change for consumers).

## Code References
- External: `marklauter/plumber.serilog.extensions` — `Plumber.Serilog.Console/Program.cs`, `Plumber.Serilog.Extensions.Tests/RequestHandlerSerilogExtensionsTests.cs`, README.md, instructions-for-llms.md.
- Local clone (gitignored): `.tmp/plumber.serilog.extensions/`.

## Notes
The `UseSerilogRequestLogging<TReq, TRes>` extension on `IRequestHandler` will also need updating — `IRequestHandler` no longer exists; the extension should target the concrete `RequestHandler<TReq, TRes>` instead.
