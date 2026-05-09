# Per-invocation object array allocation for injected middleware

- **Area:** MiddlewareFactory (performance)
- **Priority:** Low
- **Status:** Resolved

## Problem
`CreateInjectedMiddleware()` allocates a new `object[]` and uses `method.Invoke` (reflection) on every invocation. In high-throughput scenarios, this creates GC pressure.

## Suggested Fix
Use compiled expressions or source generators at registration time to eliminate per-invocation reflection.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:167-178` — reflection-based invocation with per-call allocations

## Notes
None.
