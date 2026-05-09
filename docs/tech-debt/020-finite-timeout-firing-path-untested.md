# Finite-timeout firing path is untested (and CTS isn't TimeProvider-aware)

- **Area:** Plumber / Plumber.Tests
- **Priority:** Low
- **Status:** Open

## Problem

Both finite-timeout overloads of `RequestHandler<TRequest, TResponse>.InvokeInternalAsync` build a timeout-driven `CancellationTokenSource`, await the pipeline, and translate timeout-induced `OperationCanceledException` into `TimeoutException` (per #008). The completes-before-timeout path is covered by `FiniteTimeoutInvokeCompletesBeforeTimeoutAsync` and `FiniteTimeoutNoCancellationTokenInvokeAsync`. The **timeout-fires** path — where the timeout CTS cancels mid-await and a `TimeoutException` propagates — has no test.

The straightforward "small `[Fact]` with a 50 ms timeout and a 10 s `Task.Delay`" approach is **not viable as-is**:

1. `RequestHandler` already takes a `TimeProvider` (#012), but the timeout CTS at `Plumber/RequestHandler{TRequest, TResponse}.cs:169` and `:182` is constructed with `new CancellationTokenSource(timeout)` — the wall-clock overload. There is no way to drive the timeout deterministically from a test, even though `FakeTimeProvider` is otherwise wired through the handler.
2. Any wall-clock test in this code path is the kind of timing-dependent fixture we have been deliberately avoiding — see #009's two-task TCS rendezvous as the established pattern for replacing real-time waits.
3. A previous draft of this entry suggested asserting `OperationCanceledException`. That is wrong post-#008: both overloads now throw `TimeoutException` (with the OCE preserved as `InnerException`). Any new test must assert against `TimeoutException`.

## Suggested Fix

Two coordinated changes — production code first, then the test.

### 1. Plumb `TimeProvider` into the timeout CTS

`CancellationTokenSource` has a `(TimeSpan delay, TimeProvider timeProvider)` constructor (.NET 8+). Use it in both finite-timeout overloads so a `FakeTimeProvider` registered in DI controls timer firing.

- `Plumber/RequestHandler{TRequest, TResponse}.cs:169` —
  `using var timeoutTokenSource = new CancellationTokenSource(timeout);`
  → `using var timeoutTokenSource = new CancellationTokenSource(timeout, timeProvider);`
- `Plumber/RequestHandler{TRequest, TResponse}.cs:182` — same change in the cancellation-token overload.

The `timeProvider` field is already on the handler (line 18) and resolved from DI at construction (line 27), so no other plumbing is needed.

### 2. Add a deterministic test using `FakeTimeProvider`

Sketch (assert `TimeoutException`, advance time deterministically, no real `Task.Delay`):

```csharp
[Fact]
public async Task FiniteTimeoutFiresAndThrowsTimeoutExceptionAsync()
{
    var fakeTime = new FakeTimeProvider();
    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    using var handler = RequestHandlerBuilder.Create<string, string>()
        .ConfigureServices((_, s) => s.AddSingleton<TimeProvider>(fakeTime))
        .Build(TimeSpan.FromSeconds(30))
        .Use(async (context, next) =>
        {
            // park until cancelled by the (fake-time-driven) timeout CTS
            using var registration = context.CancellationToken.Register(
                () => tcs.TrySetCanceled(context.CancellationToken));
            await tcs.Task;
            await next(context);
        });

    var invocation = handler.InvokeAsync("request", TestContext.Current.CancellationToken);

    fakeTime.Advance(TimeSpan.FromSeconds(31));

    var ex = await Assert.ThrowsAsync<TimeoutException>(() => invocation);
    _ = Assert.IsType<OperationCanceledException>(ex.InnerException);
}
```

Add a sibling test for the `(TRequest, CancellationToken)` overload that exercises the linked-CTS path on `:182`. The caller-cancel-wins-on-race case is already covered in #008's regression suite.

## Code References

- `Plumber/RequestHandler{TRequest, TResponse}.cs:18,27` — `timeProvider` field and DI resolution; already available
- `Plumber/RequestHandler{TRequest, TResponse}.cs:167-178` — finite-timeout no-CT overload; CTS not TimeProvider-aware
- `Plumber/RequestHandler{TRequest, TResponse}.cs:180-195` — finite-timeout with-CT overload; same issue
- `Plumber/RequestHandler{TRequest, TResponse}.cs:174-176,190-194` — exception filters from #008 — assertions must target `TimeoutException`
- `Microsoft.Extensions.TimeProvider.Testing` — already in use elsewhere in the suite via `FakeTimeProvider`

## Notes

Carved out of the now-closed #018. Effort is now closer to a half-day than 15 minutes — production-code change touches public timing semantics in two methods, plus a pair of deterministic tests. No public API surface changes (the new CTS overload is an implementation detail), but the change does mean a user-supplied `TimeProvider` now controls timeout firing as well as `RequestContext.Elapsed`. That is arguably the correct, consistent behavior, but worth a one-line note in the `Timeout` property remarks (`:35-39`) when the fix lands.
