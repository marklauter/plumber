# AddUserSecrets uses the wrong assembly

- **Area:** RequestHandlerBuilder (configuration)
- **Priority:** High
- **Status:** Resolved

## Problem
`AddUserSecrets(Assembly.GetExecutingAssembly(), true, true)` returns the **Plumber library assembly**, not the consumer's application assembly. User secrets are keyed by `UserSecretsId` in the consumer's `.csproj`, so this will look for secrets on the Plumber NuGet package assembly — which never has any. This is a silent failure because `optional: true` suppresses exceptions.

## Suggested Fix
Use `Assembly.GetEntryAssembly()` or accept the assembly as a parameter from the consumer.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:29` — `Assembly.GetExecutingAssembly()` used for user secrets

## Notes
None.

## Resolution
`AddDefaultConfigurationSources()` no longer calls `AddUserSecrets` — a library default cannot honestly know the consumer's assembly. Consumers opt in explicitly via the new direct method `AddUserSecrets<T>()`, which uses `T`'s assembly (the consumer's, not Plumber's) to resolve `UserSecretsIdAttribute`. The wrong-assembly call site is removed entirely.
