# User secrets loaded unconditionally in all environments

- **Area:** RequestHandlerBuilder (configuration)
- **Priority:** Low
- **Status:** Resolved

## Problem
The doc comments say "if ENV == DEV then AddUserSecrets" but the code calls `AddUserSecrets` unconditionally with no environment check. Production environments should not attempt to load developer secrets.

## Suggested Fix
Guard with an environment check, or remove the unconditional call and let consumers opt in.

## Code References
- `Plumber/RequestHandlerBuilder{TRequest, TResponse}.cs:29` — Unconditional `AddUserSecrets` call

## Notes
Related to 004 — even if the assembly issue is fixed, this would still load secrets in production.

## Resolution
Resolved alongside #004. `AddDefaultConfigurationSources()` no longer loads user secrets at all, so there's nothing to gate on environment. Consumers add user secrets explicitly via `AddUserSecrets<T>()`, where they can apply their own environment guard if desired.
