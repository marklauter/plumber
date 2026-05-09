# Build() leaks IConfigurationRoot when a post-Build step throws

- **Area:** RequestHandlerBuilder (configuration lifecycle)
- **Priority:** Medium
- **Status:** Resolved

## Problem
`RequestHandlerBuilder<TRequest, TResponse>.Build(TimeSpan)` constructs an `IConfigurationRoot` (which owns file watchers when sources use `reloadOnChange: true`) before the DI container's disposal-capture path is wired up. If anything between `perBuild.Build()` and the `RequestHandler` ctor throws, the configuration root is orphaned and its file watchers leak.

The leak window covers three failure modes:
1. A `ConfigureLogging` callback throws.
2. A `ConfigureServices` callback throws.
3. `BuildServiceProvider()` throws (e.g. options validation, scope-validation diagnostic).

DI only captures the configuration singleton for disposal once `serviceCollection.BuildServiceProvider()` runs inside the `RequestHandler` ctor. Until then, the `IConfigurationRoot` is held only by the local variable.

This is distinct from #002, which addressed happy-path disposal. #019 is the exceptional-exit case.

## Suggested Fix
Wrap the post-Build section in `try`/`catch`. On failure, dispose the configuration before rethrowing:

```csharp
var configuration = perBuild.Build();
try
{
    var serviceCollection = new ServiceCollection();
    _ = serviceCollection.AddSingleton<IConfiguration>(_ => configuration);
    ApplyLoggingCallbacks(serviceCollection);
    ApplyServiceCallbacks(serviceCollection, configuration);
    return new RequestHandler<TRequest, TResponse>(serviceCollection, timeout);
}
catch
{
    (configuration as IDisposable)?.Dispose();
    throw;
}
```

`ConfigurationRoot.Dispose()` iterates its providers and disposes those that implement `IDisposable`, releasing file watchers.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs` — `Build(TimeSpan)` method
- `Plumber/RequestHandler{TRequest, TResponse}.cs` — ctor where `BuildServiceProvider()` finally captures the singleton

## Notes
Edge case but real. Affects any consumer that uses `reloadOnChange: true` (including the defaults registered by `AddDefaultConfigurationSources`) and has a `ConfigureServices` callback that can fail. Testable by injecting a custom `IConfigurationSource` whose provider records dispose.

## Resolution
`Build(TimeSpan)` wraps the post-`perBuild.Build()` section in `try`/`catch` and disposes the `IConfigurationRoot` before rethrowing.

Regression tests added in `Plumber.Tests/PlumberTests.cs` (`RequestHandlerBuilderTests`):
- `BuildDisposesConfigurationWhenServiceCallbackThrows` — `ConfigureServices` failure path.
- `BuildDisposesConfigurationWhenLoggingCallbackThrows` — `ConfigureLogging` failure path.

Both use a `DisposalProbeSource`/`DisposalProbeProvider` pair that records when `ConfigurationRoot.Dispose()` cascades to its providers. Verified to fail before the fix and pass after.
