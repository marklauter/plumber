using OpenTelemetry.Metrics;
using Plumber;
using System.Diagnostics;

namespace Sample.Cli.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task ValidInputProducesFullReportAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync("Hello, World! FOO", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Null(report.ErrorMessage);
        Assert.Equal("Hello, World! FOO", report.Original);
        Assert.Equal("hello, world! foo", report.Normalized);
        Assert.Equal(["hello,", "world!", "foo"], report.Tokens);
        Assert.Equal(3, report.WordCount);
        Assert.True(report.Elapsed > TimeSpan.Zero, "timing middleware should record positive elapsed time");
    }

    [Fact]
    public async Task EmptyInputShortCircuitsAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Equal("input must be non-empty", report.ErrorMessage);
        Assert.Empty(report.Tokens);
        Assert.Equal(0, report.WordCount);
    }

    [Fact]
    public async Task WhitespaceInputShortCircuitsAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync("   \t  ", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Equal("input must be non-empty", report.ErrorMessage);
    }

    [Fact]
    public void PipelineRegistersMiddlewareInOrder()
    {
        using var handler = Pipeline.Build([]);

        Assert.Collection(
            handler.Middleware,
            // the OpenTelemetry and Serilog middleware are internal to their extension packages, so assert by type name
            m => Assert.StartsWith("RequestTracingMiddleware", m.MiddlewareType!.Name, StringComparison.Ordinal),
            m => Assert.StartsWith("RequestMetricsMiddleware", m.MiddlewareType!.Name, StringComparison.Ordinal),
            m => Assert.StartsWith("RequestLoggerMiddleware", m.MiddlewareType!.Name, StringComparison.Ordinal),
            m => Assert.Equal(MiddlewareDescriptor.DelegateDisplayName, m.DisplayName), // the timing delegate
            m => Assert.Equal(typeof(ValidationMiddleware), m.MiddlewareType),
            m => Assert.Equal(typeof(NormalizeMiddleware), m.MiddlewareType),
            m => Assert.Equal(typeof(TokenizeMiddleware), m.MiddlewareType),
            m => Assert.Equal(typeof(ReportMiddleware), m.MiddlewareType));
    }

    [Fact]
    public async Task TelemetrySummaryReflectsAPipelineRunAsync()
    {
        List<Activity> spans = [];
        List<Metric> metrics = [];
        using var tracerProvider = Telemetry.CreateTracerProvider(spans);
        using var meterProvider = Telemetry.CreateMeterProvider(metrics);
        using var handler = Pipeline.Build([]);

        _ = await handler.InvokeAsync("Hello, World!", TestContext.Current.CancellationToken);

        _ = meterProvider.ForceFlush();
        var summary = Telemetry.Summarize(spans, metrics);

        Assert.Contains("Plumber.HandleRequest", summary, StringComparison.Ordinal);
        Assert.Contains("plumber.requests.count", summary, StringComparison.Ordinal);
        Assert.Contains("plumber.requests.duration", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortCircuitStillRecordsElapsedAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.True(report.Elapsed > TimeSpan.Zero, "timing should wrap validation short-circuit");
    }
}
