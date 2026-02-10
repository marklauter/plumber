# AddUserSecrets uses the wrong assembly

- **Area:** RequestHandlerBuilder (configuration)
- **Priority:** High
- **Status:** Open

## Problem
`AddUserSecrets(Assembly.GetExecutingAssembly(), true, true)` returns the **Plumber library assembly**, not the consumer's application assembly. User secrets are keyed by `UserSecretsId` in the consumer's `.csproj`, so this will look for secrets on the Plumber NuGet package assembly — which never has any. This is a silent failure because `optional: true` suppresses exceptions.

## Suggested Fix
Use `Assembly.GetEntryAssembly()` or accept the assembly as a parameter from the consumer.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:29` — `Assembly.GetExecutingAssembly()` used for user secrets

## Notes
None.
