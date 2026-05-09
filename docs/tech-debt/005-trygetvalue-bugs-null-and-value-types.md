# TryGetValue has incorrect behavior for null values and value types

- **Area:** RequestContext (data dictionary)
- **Priority:** Medium
- **Status:** Resolved

## Problem
The `TryGetValue<T>` method has two bugs:

1. If a user stores `context.Data["key"] = null`, calling `TryGetValue<string>("key", out var val)` returns `false` because the `(item = (T?)value) != null` check fails, even though the key exists.

2. For value types like `int`, when the key does NOT exist, the fallback `(item = default) != null` evaluates to `true` (since `default(int)` is `0`, which is not null), so the method returns `true` with `item = 0`. This is the opposite of the intended behavior.

## Suggested Fix
Rewrite as a standard method body using `value is T typed` pattern matching.

## Resolution
Rewritten using the `value is T typed` pattern. Behavior now:
- Missing key → `false`, `item = default(T)`
- Stored null → `false` (consistent with `[NotNullWhen(true)]`)
- Type mismatch → `false`, no throw (was `InvalidCastException`)
- Match → `true`, `item` is the typed value

XML docs cleaned up inline (removed misleading `<remarks>`, filled in `<param name="item">`, fixed `<returns>`).

Regression tests added in `Plumber.Tests/PlumberTests.cs`:
- `TryGetValueFalseWhenValueTypeKeyNotFound` — pins Bug 1.
- `TryGetValueTrueForStoredValueType` — round-trip for value types.
- `TryGetValueFalseWhenStoredValueIsNull` — pins null-value semantics.
- `TryGetValueFalseOnTypeMismatchDoesNotThrow` — pins no-throw contract.

## Code References
- `Plumber/RequestContext{TRequest, TResponse}.cs` — `TryGetValue<T>` implementation

## Notes
None.
