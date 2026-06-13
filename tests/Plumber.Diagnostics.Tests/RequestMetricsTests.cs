using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Diagnostics.Tests;

public sealed class RequestMetricsTests
{
    private const string CountInstrument = "plumber.requests.count";
    private const string DurationInstrument = "plumber.requests.duration";
    private const string ErrorInstrument = "plumber.requests.errors";

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task RecordsCountAndDurationOnSuccessAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestMetrics()
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        var count = Assert.Single(metrics.Measurements, m => m.Instrument == CountInstrument);
        Assert.Equal(1, count.Value);
        Assert.Equal(nameof(TestRequest), count.Tags["request.type"]);

        var duration = Assert.Single(metrics.Measurements, m => m.Instrument == DurationInstrument);
        Assert.Equal(true, duration.Tags["success"]);
        Assert.DoesNotContain(metrics.Measurements, m => m.Instrument == ErrorInstrument);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task RecordsErrorAndDurationOnExceptionAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        var boom = new InvalidOperationException("boom");
        _ = handler
            .UseRequestMetrics()
            .Use((_, _) => throw boom);

        // ThrowOnException defaults to true, so the original exception surfaces to the caller unchanged.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
        _ = Assert.Single(metrics.Measurements, m => m.Instrument == ErrorInstrument);
        var duration = Assert.Single(metrics.Measurements, m => m.Instrument == DurationInstrument);
        Assert.Equal(false, duration.Tags["success"]);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task RecordCustomMetricsReceivesSuccessTrueAsync()
    {
        using var handler = TestPipeline.CreateHandler();

        bool? observed = null;
        _ = handler
            .UseRequestMetrics(options => options.RecordCustomMetrics = (_, success) => observed = success)
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.True(observed);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task RecordCustomMetricsReceivesSuccessFalseOnSwallowedExceptionAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        bool? observed = null;
        _ = handler
            .UseRequestMetrics(options =>
            {
                options.ThrowOnException = false;
                options.RecordCustomMetrics = (_, success) => observed = success;
            })
            .Use((_, _) => throw new InvalidOperationException("boom"));

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.False(observed);
        _ = Assert.Single(metrics.Measurements, m => m.Instrument == ErrorInstrument);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task AddDefaultMetricsFalseOmitsDefaultsButRunsCustomOnSuccessAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        var customRan = false;
        _ = handler
            .UseRequestMetrics(options =>
            {
                options.AddDefaultMetrics = false;
                options.RecordCustomMetrics = (_, _) => customRan = true;
            })
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.True(customRan);
        Assert.Empty(metrics.Measurements);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task AddDefaultMetricsFalseOmitsDefaultsOnExceptionAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestMetrics(options =>
            {
                options.AddDefaultMetrics = false;
                options.ThrowOnException = false;
            })
            .Use((_, _) => throw new InvalidOperationException("boom"));

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Empty(metrics.Measurements);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestMetrics/Use return the same handler instance; the using var owns disposal")]
    public async Task ParameterlessMetricsResolvesOptionsFromDiAsync()
    {
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler(services =>
            services.AddPlumberDiagnostics<TestRequest, TestResponse>(
                configureMetrics: options => options.AddDefaultMetrics = true));

        _ = handler
            .UseRequestMetrics()
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        _ = Assert.Single(metrics.Measurements, m => m.Instrument == CountInstrument);
    }

    [Fact]
    public async Task MiddlewareGuardsAgainstNullArgumentsAsync()
    {
        var options = Options.Create(new RequestMetricsOptions<TestRequest, TestResponse>());
        RequestMiddleware<TestRequest, TestResponse> next = _ => Task.CompletedTask;

        _ = Assert.Throws<ArgumentNullException>(() => new RequestMetricsMiddleware<TestRequest, TestResponse>(null!, options));
        _ = Assert.Throws<ArgumentNullException>(() => new RequestMetricsMiddleware<TestRequest, TestResponse>(next, null!));

        var middleware = new RequestMetricsMiddleware<TestRequest, TestResponse>(next, options);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }
}
