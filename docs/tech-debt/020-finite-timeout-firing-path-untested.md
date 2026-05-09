# Finite-timeout firing path is untested (and CTS isn't TimeProvider-aware)

- **Area:** Plumber / Plumber.Tests
- **Priority:** Low
- **Status:** Resolved

## Problem

Both finite-timeout overloads of `RequestHandler<TRequest, TResponse>.InvokeInternalAsync` build a timeout-driven `CancellationTokenSource`, await the pipeline, and translate timeout-induced `OperationCanceledException` into `TimeoutException` (per #008). The completes-before-timeout path was covered, but the **timeout-fires** path had no test, because the timeout CTS was constructed with the wall-clock `new CancellationTokenSource(timeout)` overload — ignoring the handler's `TimeProvider` (#012) and leaving no deterministic way to drive the timer.

## Resolution

Two-line production change plus a deterministic-test pair using `FakeTimeProvider`.

**Production:** both finite-timeout `InvokeInternalAsync` overloads now construct the CTS with `new CancellationTokenSource(timeout, timeProvider)` (.NET 8+ overload). The `timeProvider` field was already on the handler; no other plumbing was needed. `RequestHandlerBuilder.Build` already registers `TimeProvider.System` via `TryAddSingleton` after user `ConfigureServices` callbacks, so a user- or test-supplied `TimeProvider` wins automatically.

**Doc:** the `Timeout` property `<remarks>` now notes that the registered `TimeProvider` drives the timer, making the test pattern discoverable.

**Tests:** two new `[Fact]` methods in `Plumber.Tests/PlumberTests.cs` — one per finite-timeout overload — register a `FakeTimeProvider`, park the middleware on a `TaskCompletionSource` (with `RunContinuationsAsynchronously` to avoid reentrancy with `Advance`'s synchronous callback chain), call `fakeTime.Advance(...)` past the timeout, and assert `TimeoutException` with `OperationCanceledException` as `InnerException`. No real `Task.Delay`; both tests complete in microseconds (full pair runs in ~50 ms cold).

**Package:** `Microsoft.Extensions.TimeProvider.Testing` added to `Directory.Packages.props` (pinned at `10.1.0` — `10.0.7` is not published for this package; rest of the M.E.* family stays at `10.0.7`) and referenced from `Plumber.Tests.csproj`.

## Code References

- `Plumber/RequestHandler{TRequest, TResponse}.cs:36-40` — `Timeout` property remarks (TimeProvider note added)
- `Plumber/RequestHandler{TRequest, TResponse}.cs:169` — finite-timeout no-CT overload, CTS now TimeProvider-aware
- `Plumber/RequestHandler{TRequest, TResponse}.cs:182` — finite-timeout with-CT overload, same change
- `Plumber.Tests/PlumberTests.cs` — `FiniteTimeoutFiresAndThrowsTimeoutExceptionAsync` and `FiniteTimeoutFiresAndThrowsTimeoutExceptionWithCallerTokenAsync` (added between the existing #008 cluster and `CallerCancellationStillThrowsOperationCanceledWhenTimeoutConfiguredAsync`)
- `Directory.Packages.props` — `Microsoft.Extensions.TimeProvider.Testing` 10.1.0
- `Plumber.Tests/Plumber.Tests.csproj` — `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />`

## Notes

Carved out of the now-closed #018. Surface impact: a user-supplied `TimeProvider` now controls timeout firing as well as `RequestContext.Elapsed` — consistent with #012's intent and documented on the property.

The two wall-clock #008 tests (`TimeoutWithoutCallerTokenThrowsTimeoutExceptionAsync` and `TimeoutWithUncancelledCallerTokenThrowsTimeoutExceptionAsync`) were superseded by the new deterministic pair and removed — they exercised the same overloads with the same assertions, just non-deterministically. The remaining #008 tests stay as-is: `CallerCancellationStillThrowsOperationCanceledWhenTimeoutConfiguredAsync` exercises a pre-cancelled caller token (timeout never fires; wall-clock fine), and `CallerCancellationWinsRaceAgainstTimeoutAsync` is an explicit real-concurrency race test that needs real time by design.
