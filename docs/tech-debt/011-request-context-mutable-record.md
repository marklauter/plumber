# RequestContext is a mutable record

- **Area:** RequestContext (type design)
- **Priority:** Low
- **Status:** Open

## Problem
`RequestContext` is a `record` with mutable state (`Response` setter, lazy `Data` dictionary). Records provide value-based equality, but mutation makes `Equals`/`GetHashCode` unstable. If used in a collection, hash lookups would break after mutation.

## Suggested Fix
Change to `class` or override equality to use only immutable fields.

## Code References
- `Plumber/RequestContext{TRequest, TResponse}.cs` — record definition with mutable properties

## Notes
None.
