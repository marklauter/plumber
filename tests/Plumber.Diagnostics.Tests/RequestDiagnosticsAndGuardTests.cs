using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Diagnostics.Tests;

public sealed class RequestDiagnosticsAndGuardTests
{
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestDiagnostics/Use return the same handler instance; the using var owns disposal")]
    public async Task RequestDiagnosticsRegistersBothTracingAndMetricsAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestDiagnostics()
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        _ = Assert.Single(spans.Activities);
        _ = Assert.Single(metrics.Measurements, m => m.Instrument == "plumber.requests.count");
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseRequestDiagnostics/Use return the same handler instance; the using var owns disposal")]
    public async Task RequestDiagnosticsWithConfigurationAppliesBothOptionsAsync()
    {
        using var spans = new ActivityCollector(PlumberDiagnostics.ActivitySourceName);
        using var metrics = new MeterCollector(PlumberDiagnostics.MeterName);
        using var handler = TestPipeline.CreateHandler();

        _ = handler
            .UseRequestDiagnostics(
                tracing => tracing.OperationName = "MonitorOperation",
                metric => metric.AddDefaultMetrics = true)
            .Use(TestPipeline.Succeeds);

        _ = await handler.InvokeAsync(new TestRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("MonitorOperation", Assert.Single(spans.Activities).OperationName);
        _ = Assert.Single(metrics.Measurements, m => m.Instrument == "plumber.requests.count");
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP005:Return type should indicate that the value should be disposed",
        Justification = "the guarded extension throws ArgumentNullException before constructing anything; no disposable is created")]
    public void ExtensionsGuardAgainstNullHandler()
    {
        RequestHandler<TestRequest, TestResponse> handler = null!;

        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestTracing());
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestTracing(_ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestMetrics());
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestMetrics(_ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestDiagnostics());
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestDiagnostics(_ => { }, _ => { }));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "the using var owns disposal; the guarded calls throw before returning a handler")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP005:Return type should indicate that the value should be disposed",
        Justification = "the guarded extension throws ArgumentNullException before constructing anything; no disposable is created")]
    public void ExtensionsGuardAgainstNullConfiguration()
    {
        using var handler = TestPipeline.CreateHandler();

        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestTracing(null!));
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestMetrics(null!));
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestDiagnostics(null!, _ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => handler.UseRequestDiagnostics(_ => { }, null!));
    }

    [Fact]
    public void AddPlumberDiagnosticsGuardsAgainstNullServices()
    {
        IServiceCollection services = null!;
        _ = Assert.Throws<ArgumentNullException>(() => services.AddPlumberDiagnostics<TestRequest, TestResponse>());
    }
}
