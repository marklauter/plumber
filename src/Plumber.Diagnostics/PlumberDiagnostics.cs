namespace Plumber.Diagnostics;

/// <summary>
/// Names of the <see cref="System.Diagnostics.ActivitySource"/> and <see cref="System.Diagnostics.Metrics.Meter"/>
/// the middleware emits through. Pass these to the OpenTelemetry SDK so it collects Plumber telemetry, e.g.
/// <c>AddSource(PlumberDiagnostics.ActivitySourceName)</c> and <c>AddMeter(PlumberDiagnostics.MeterName)</c>.
/// </summary>
public static class PlumberDiagnostics
{
    /// <summary>The name of the <see cref="System.Diagnostics.ActivitySource"/> the tracing middleware starts spans on.</summary>
    public const string ActivitySourceName = "Plumber.Diagnostics";

    /// <summary>The name of the <see cref="System.Diagnostics.Metrics.Meter"/> the metrics middleware records instruments on.</summary>
    public const string MeterName = "Plumber.Diagnostics";
}
