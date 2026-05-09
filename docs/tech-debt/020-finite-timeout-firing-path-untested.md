# Finite-timeout firing path is untested

- **Area:** Plumber.Tests
- **Priority:** Low
- **Status:** Open

## Problem
`RequestHandler<TRequest, TResponse>.InvokeInternalAsync(TRequest, TimeSpan, CancellationToken)` builds a linked CTS that combines the caller's token with a timeout-driven CTS, then awaits the pipeline. The completes-before-timeout path is covered by `FiniteTimeoutInvokeCompletesBeforeTimeoutAsync` and `FiniteTimeoutNoCancellationTokenInvokeAsync`, but the **timeout-firing** path — where the timeout CTS cancels mid-await and the cancellation propagates out as `OperationCanceledException` — has no test.

This is the only behavioral gap remaining after the broader coverage push that closed item #018 (resolved, deleted). All other surfaces are either tested or marked `[ExcludeFromCodeCoverage]` with justification.

## Suggested Fix
Add a single test:

```csharp
[Fact]
public async Task FiniteTimeoutFiresAndCancelsInvocationAsync()
{
    using var handler = RequestHandlerBuilder.Create<string, string>()
        .Build(TimeSpan.FromMilliseconds(50))
        .Use(async (context, next) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), context.CancellationToken);
            await next(context);
        });

    _ = await Assert.ThrowsAsync<OperationCanceledException>(
        () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
}
```

The 200x ratio between the delay and the timeout makes the test robust on slow CI; the only failure mode is a CI worker pausing for >10s, which would impact every test in the suite, not just this one.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:131` — finite-timeout `InvokeInternalAsync` with linked CTS

## Notes
Carved out of the now-closed #018. Standalone because the timing-sensitive nature warrants its own discussion if/when it lands.
