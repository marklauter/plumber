# TryGetValue has incorrect behavior for null values and value types

- **Area:** RequestContext (data dictionary)
- **Priority:** Medium
- **Status:** Open

## Problem
The `TryGetValue<T>` method has two bugs:

1. If a user stores `context.Data["key"] = null`, calling `TryGetValue<string>("key", out var val)` returns `false` because the `(item = (T?)value) != null` check fails, even though the key exists.

2. For value types like `int`, when the key does NOT exist, the fallback `(item = default) != null` evaluates to `true` (since `default(int)` is `0`, which is not null), so the method returns `true` with `item = 0`. This is the opposite of the intended behavior.

## Suggested Fix
Rewrite as a standard method body using `value is T typed` pattern matching.

## Code References
- `Plumber/RequestContext{TRequest, TResponse}.cs:39-40` — `TryGetValue<T>` implementation

## Notes
None.
