using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Plumber.Diagnostics;

namespace Sample.Cli;

/// <summary>
/// Host-free OpenTelemetry bootstrap: builds the tracer and meter providers that collect and export the
/// pipeline's spans and metrics. The providers are built eagerly so their listeners are active for the
/// middleware's <see cref="System.Diagnostics.ActivitySource"/> and <see cref="System.Diagnostics.Metrics.Meter"/>,
/// and the caller disposes them at exit to flush the console exporter.
/// </summary>
internal static class Telemetry
{
    public const string ServiceName = "Sample.Cli";

    public static TracerProvider CreateTracerProvider() =>
        Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .AddSource(PlumberDiagnostics.ActivitySourceName)
            .AddConsoleExporter()
            .Build();

    public static MeterProvider CreateMeterProvider() =>
        Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .AddMeter(PlumberDiagnostics.MeterName)
            .AddConsoleExporter()
            .Build();
}
