using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests.Performance;

// Allocation ratchet for the pipeline hot path. Per-request allocation byte-counts are
// deterministic — object sizes don't vary with GC mode (workstation/server) or JIT tier —
// so they make a stable CI gate where wall-clock time would be flaky. Each budget is an
// upper bound: lower it as allocations shrink; investigate before ever raising one.
public sealed class AllocationTests
{
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent .Use() returns the same handler instance, already owned by the using-scoped local")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using",
        Justification = "the invocation is blocked on deliberately (GetAwaiter().GetResult) so every allocation is attributed to the calling thread; the handler disposes synchronously because the pipeline registers no async-only-disposable services")]
    public void InvokeStaysWithinAllocationBudgetForSingleMiddleware()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();
        _ = handler.Use((context, next) =>
        {
            context.Response = context.Request;
            return next(context);
        });

        var bytesPerInvoke = MeasureBytesPerInvoke(handler);

        // Baseline at time of writing: 368 bytes (RequestContext + per-request DI scope + Task machinery).
        Assert.True(
            bytesPerInvoke <= 512,
            $"per-request allocation was {bytesPerInvoke} bytes, exceeding the 512-byte budget");
    }

    // Bytes allocated per synchronous invocation, measured on the calling thread.
    // The middleware completes synchronously, so InvokeAsync finishes inline and every allocation
    // is attributed to this thread — GetAllocatedBytesForCurrentThread is therefore immune to
    // allocations from xUnit tests running in parallel. Taking the minimum across rounds discards
    // transient noise (stray bookkeeping allocations only ever add to the delta).
    private static long MeasureBytesPerInvoke(RequestHandler<string, string> handler)
    {
        const int warmup = 10_000; // triggers the lazy pipeline build + JIT before measuring
        const int iterations = 20_000;
        const int rounds = 5;

        var cancellationToken = TestContext.Current.CancellationToken;

        for (var i = 0; i < warmup; i++)
        {
            Drain(handler, cancellationToken);
        }

        var best = long.MaxValue;
        for (var round = 0; round < rounds; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
            {
                Drain(handler, cancellationToken);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            best = Math.Min(best, after - before);
        }

        return best / iterations;
    }

    private static void Drain(RequestHandler<string, string> handler, CancellationToken cancellationToken) =>
        handler.InvokeAsync("x", cancellationToken).GetAwaiter().GetResult();
}
