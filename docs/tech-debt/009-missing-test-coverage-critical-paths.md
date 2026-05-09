# Missing test coverage for critical paths

- **Area:** Plumber.Tests
- **Priority:** Medium
- **Status:** Resolved

## Problem
No tests cover: timeout behavior, disposal (`ObjectDisposedException`), cancellation, error cases in middleware registration (adding after build, invalid middleware classes), concurrent invocations, configuration loading, the `Create(args, configure)` overload, or the `Build(TimeSpan)` overload. Only happy-path middleware execution is tested.

## Resolution
Coverage closed across batches A/B/C and follow-on test passes:
- **Timeout behavior** — `FiniteTimeout*` plus four `Timeout*` / `CallerCancellation*` tests added with #008.
- **Disposal** — `InvokeAsyncAfterDisposeThrowsObjectDisposedAsync`, `UseAfterDisposeThrowsObjectDisposed`, `DoubleDisposeIsSafe`.
- **Cancellation** — `TerminalShortCircuitsWhenTokenAlreadyCancelledAsync` plus the caller-cancel race tests.
- **Middleware registration errors** — `UseAfterFirstInvokeThrowsInvalidOperationAsync` (after-build) and three `UseClassMiddleware*Throws` tests covering missing `InvokeAsync`, wrong return type, and wrong first parameter.
- **Configuration loading** — three configuration tests plus `BuildTwiceProducesIndependentHandlersWithPerBuildSnapshotAsync` for #015 recipe semantics.
- **`Build(TimeSpan)` overload** — `FiniteTimeout*` tests.
- **`Create(args, configure)` overload** — N/A; that overload no longer exists post-refactor. The two surviving `Create` overloads are both covered.
- **Concurrent invocations** — one smoke test (`TwoInFlightInvocationsCompleteWithoutDeadlockAsync`) confirms two in-flight invocations don't deadlock and produce independent results. We deliberately do *not* test the build-once-under-contention invariant: that is delegated to `Lazy<T>` in its default `ExecutionAndPublication` mode and is a BCL contract, not ours to verify. Plumber owns no shared mutable state across invocations — `RequestContext` and `IServiceScope` are constructed per call from local variables — so there is no concurrency invariant of our own that warrants a runtime test.

Coverage report after these additions: 99%+ line coverage on `Plumber`. The two remaining gaps are defensive paths (`(configuration as IDisposable)?.Dispose()` null branch and the `AddLogging` lambda body when no consumer resolves `ILoggerFactory`).

## Code References
- `Plumber.Tests/PlumberTests.cs`
- `Plumber.Tests/Middleware/NoInvokeAsyncMiddleware.cs`
- `Plumber.Tests/Middleware/WrongReturnTypeMiddleware.cs`
- `Plumber.Tests/Middleware/WrongFirstParamMiddleware.cs`

## Notes
None.
