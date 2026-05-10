# Testing

## Philosophy

**Don't test what you don't own.** Tests exercise Plumber's behavior, not `Microsoft.Extensions.*` or the .NET runtime. No assertions on how DI resolves a service, how `IConfiguration` parses JSON, or how `ILogger` formats a message — focus on the contract: given a pipeline configuration and a request, does Plumber produce the right response and side effects?

**Test the contract, not the construction.** Assert on what a method promises, not how it does it. No assertions on private state or internal calls — refactoring internals should not break tests.

**Tests are documentation.** A test name describes the scenario and the guaranteed outcome. Method names are PascalCase (e.g. `BuildDisposesConfigurationWhenServiceCallbackThrows`); `IDE1006` is suppressed in test projects so longer descriptive names are fine.

## Setup

Any project named `*.Tests` is auto-configured by [`Directory.Build.props`](../../Directory.Build.props): `xunit.v3`, runner, test SDK, coverlet, `IsTestProject`, and the `Xunit` global using are applied automatically. Add a `ProjectReference` to the system under test and start writing tests.

## Conventions

- xUnit v3 — never reference legacy `xunit`, `xunit.core`, or `xunit.assert`.
- Pass `TestContext.Current.CancellationToken` to any method that accepts one (xUnit1051).
- Versions are managed centrally in `Directory.Packages.props` — don't pin in csprojs.
- Run with `dotnet test`.
