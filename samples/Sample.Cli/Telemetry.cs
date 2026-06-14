using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Plumber.Diagnostics;
using System.Diagnostics;

namespace Sample.Cli;

/// <summary>
/// Host-free OpenTelemetry bootstrap: builds the tracer and meter providers that collect the pipeline's
/// spans and metrics into the supplied lists. The providers are built eagerly so their listeners are active
/// for the middleware's <see cref="ActivitySource"/> and <see cref="System.Diagnostics.Metrics.Meter"/>, and
/// the caller disposes them at exit. The in-memory exporter keeps the demo console legible — instead of
/// dumping every span and metric, <see cref="Summarize"/> prints a one-line confirmation that collection worked.
/// </summary>
internal static class Telemetry
{
    public const string ServiceName = "Sample.Cli";

    public static TracerProvider CreateTracerProvider(ICollection<Activity> exportedSpans) =>
        Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .AddSource(PlumberDiagnostics.ActivitySourceName)
            .AddInMemoryExporter(exportedSpans)
            .Build();

    public static MeterProvider CreateMeterProvider(ICollection<Metric> exportedMetrics) =>
        Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .AddMeter(PlumberDiagnostics.MeterName)
            .AddInMemoryExporter(exportedMetrics)
            .Build();

    /// <summary>
    /// Renders a compact summary of the spans and metrics the in-memory exporters collected, so a sample run
    /// shows that OpenTelemetry collection worked without the full exporter dump.
    /// </summary>
    public static string Summarize(IReadOnlyCollection<Activity> spans, IReadOnlyCollection<Metric> metrics)
    {
        List<string> lines = [$"opentelemetry: {spans.Count} span(s) collected via the in-memory exporter"];
        lines.AddRange(spans.Select(span => $"  span   {span.OperationName} ({span.Status})"));
        lines.AddRange(metrics.Select(DescribeMetric));
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeMetric(Metric metric)
    {
        if (metric.MetricType == MetricType.Histogram)
        {
            long samples = 0;
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                samples += point.GetHistogramCount();
            }

            return $"  metric {metric.Name} ({samples} sample(s))";
        }

        long sum = 0;
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            sum += point.GetSumLong();
        }

        return $"  metric {metric.Name} = {sum}";
    }
}
