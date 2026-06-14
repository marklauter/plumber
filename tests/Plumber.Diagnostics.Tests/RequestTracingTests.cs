using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Diagnostics.Tests;

public sealed class RequestTracingTests
{
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task TracksSuccessfulRequestWithDefaultAttributesAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options => options.OperationName = "TestOperation")
            .Use(TestPipeline.Succeeds);

        var response = await handler.InvokeAsync(new TestRequest { Value = "hi" }, TestContext.Current.CancellationToken);

        Assert.True(response!.Success);
        var activity = Assert.Single(spans.Activities);
        Assert.Equal("TestOperation", activity.OperationName);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.NotNull(activity.GetTagItem("request.id"));
        Assert.Equal(nameof(TestRequest), activity.GetTagItem("request.type"));
        Assert.NotNull(activity.GetTagItem("request.elapsed_ms"));
        Assert.Equal(nameof(TestResponse), activity.GetTagItem("response.type"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task EnrichSpanAddsCustomTagAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options => options.EnrichSpan = (activity, context) =>
                activity.SetTag("request.value", context.Request.Value))
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest { Value = "enriched" }, TestContext.Current.CancellationToken);

        var activity = Assert.Single(spans.Activities);
        Assert.Equal("enriched", activity.GetTagItem("request.value"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task SpanKindIsAppliedAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options => options.SpanKind = ActivityKind.Server)
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(ActivityKind.Server, Assert.Single(spans.Activities).Kind);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task DownstreamExceptionIsRecordedAndRethrownAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        var boom = new InvalidOperationException("boom");
        _ = handler
            .UseRequestTracing()
            .Use((_, _) => throw boom);

        // ThrowOnException defaults to true, so the original exception surfaces to the caller unchanged.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
        var activity = Assert.Single(spans.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Contains(activity.Events, e => e.Name == "exception");
        Assert.NotNull(activity.GetTagItem("request.elapsed_ms"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task ThrowOnExceptionFalseSwallowsAndRecordsErrorAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options =>
            {
                options.ThrowOnException = false;
                options.EnrichSpan = (activity, _) => activity.SetTag("enriched", true);
            })
            .Use((_, _) => throw new InvalidOperationException("boom"));

        var response = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Null(response);
        var activity = Assert.Single(spans.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        // EnrichSpan runs on the failure path too.
        Assert.Equal(true, activity.GetTagItem("enriched"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task RecordExceptionFalseLeavesStatusUnsetAndAddsNoEventAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options =>
            {
                options.RecordException = false;
                options.ThrowOnException = false;
            })
            .Use((_, _) => throw new InvalidOperationException("boom"));

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(spans.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.DoesNotContain(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task AddDefaultAttributesFalseOmitsDefaultTagsAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options => options.AddDefaultAttributes = false)
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(spans.Activities);
        Assert.Null(activity.GetTagItem("request.id"));
        Assert.Null(activity.GetTagItem("request.type"));
        Assert.Null(activity.GetTagItem("response.type"));
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task NoListenerShortCircuitsButStillProcessesRequestAsync()
    {
        // No ActivityCollector is subscribed, so ActivitySource.StartActivity returns null and the
        // middleware must fall through to the next component without touching a span.
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing()
            .Use(TestPipeline.Succeeds);

        var response = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.True(response!.Success);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task ParameterlessTracingResolvesOptionsFromDiAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler(services =>
            services.AddPlumberDiagnostics<TestRequest, TestResponse>(
                configureTracing: options => options.OperationName = "FromDi"));

        // The parameterless overload resolves IOptions from DI, so the operation name configured via
        // AddPlumberDiagnostics must flow through to the span.
        _ = handler
            .UseRequestTracing()
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("FromDi", Assert.Single(spans.Activities).OperationName);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task CancellationLeavesSpanUnsetAndRethrowsAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing()
            .Use((_, _) => throw new OperationCanceledException());

        // Cancellation is not a defect: it propagates (ThrowOnException defaults true), but the span stays Unset.
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken));

        var activity = Assert.Single(spans.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.DoesNotContain(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task CancellationWithThrowOnExceptionFalseSwallowsAndLeavesSpanUnsetAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestTracing(options =>
            {
                options.ThrowOnException = false;
                options.EnrichSpan = (activity, _) => activity.SetTag("enriched", true);
            })
            .Use((_, _) => throw new OperationCanceledException());

        var response = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Null(response);
        var activity = Assert.Single(spans.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        // EnrichSpan runs on cancellation too, even though the span stays Unset.
        Assert.Equal(true, activity.GetTagItem("enriched"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestTracing/Use return the same handler instance; the using var owns disposal")]
    public async Task NullResponseRecordsResponseTypeNullAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var handler = TestPipeline.CreateHandler();

        // A middleware that completes without assigning a response leaves context.Response null.
        _ = handler
            .UseRequestTracing()
            .Use((_, _) => Task.CompletedTask);

        var response = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Null(response);
        Assert.Equal("null", Assert.Single(spans.Activities).GetTagItem("response.type"));
    }

    [Fact]
    public async Task MiddlewareGuardsAgainstNullArgumentsAsync()
    {
        var options = Options.Create(new RequestTracingOptions<TestRequest, TestResponse>());
        RequestMiddleware<TestRequest, TestResponse> next = _ => Task.CompletedTask;

        _ = Assert.Throws<ArgumentNullException>(() => new RequestTracingMiddleware<TestRequest, TestResponse>(null!, options));
        _ = Assert.Throws<ArgumentNullException>(() => new RequestTracingMiddleware<TestRequest, TestResponse>(next, null!));

        var middleware = new RequestTracingMiddleware<TestRequest, TestResponse>(next, options);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }
}
