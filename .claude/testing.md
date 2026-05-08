# Testing

Uses **xUnit v3** (migrated from v2 on 2026-05-08).

When creating new test projects:

- Reference `xunit.v3` (not `xunit`) — single package includes assertions, core, and runner
- Reference `xunit.runner.visualstudio` v3.x with `<PrivateAssets>all</PrivateAssets>`
- Reference `Microsoft.NET.Test.Sdk`
- Add a global using: `<Using Include="Xunit" />`
- Set `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`
- Do **not** reference legacy `xunit`, `xunit.core`, or `xunit.assert` packages

When awaiting any method that accepts a `CancellationToken`, pass `TestContext.Current.CancellationToken` so the test runner can cancel responsively (xUnit1051).

Package versions are managed centrally in `Directory.Packages.props` — do not pin versions in individual csprojs.

Run tests with `dotnet test` from the solution or project directory.
