# Reflection Invoke null result causes NullReferenceException

- **Area:** RequestHandler / MiddlewareFactory
- **Priority:** High
- **Status:** Open

## Problem
`(Task)method.Invoke(middleware, args)!` uses a null-forgiving operator. If a middleware's `InvokeAsync` returns `null`, this throws a `NullReferenceException` with no diagnostic message. The null-forgiving operator silences the compiler but does not prevent the runtime null dereference.

## Suggested Fix
Add a null check: `(Task)(method.Invoke(middleware, args) ?? throw new InvalidOperationException($"{method.DeclaringType?.FullName}.{method.Name} returned null."))`.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:177` — `(Task)method.Invoke(middleware, args)!`

## Notes
None.
