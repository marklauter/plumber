# Reflection Invoke null result causes NullReferenceException

- **Area:** RequestHandler / MiddlewareFactory
- **Priority:** High
- **Status:** Resolved

## Problem
`(Task)method.Invoke(middleware, args)!` uses a null-forgiving operator. If a middleware's `InvokeAsync` returns `null`, this throws a `NullReferenceException` with no diagnostic message. The null-forgiving operator silences the compiler but does not prevent the runtime null dereference.

## Suggested Fix
Add a null check: `(Task)(method.Invoke(middleware, args) ?? throw new InvalidOperationException($"{method.DeclaringType?.FullName}.{method.Name} returned null."))`.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs` — `MiddlewareFactory.CreateInjectedMiddleware`

## Notes
None.

## Resolution
Replaced the null-forgiving operator with a `?? throw new InvalidOperationException(...)` that names the offending method (`{DeclaringType.FullName}.{method.Name} returned null.`).

Regression test added in `Plumber.Tests/PlumberTests.cs`:
- `InjectedMiddlewareReturningNullTaskThrowsDescriptiveAsync` — uses `NullTaskMiddleware` (a class-based middleware whose `InvokeAsync` returns `null!`) plus an injected service to force the `CreateInjectedMiddleware` path. Asserts the exception is `InvalidOperationException` with a message naming the middleware type and method.

Verified the test fails before the fix (`NullReferenceException` with no message) and passes after.
