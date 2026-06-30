using OpenTelemetry.Metrics;
using Sample.Cli;
using System.Diagnostics;

var input = args.Length > 0
    ? string.Join(' ', args)
    : await Console.In.ReadToEndAsync();

// Host-free OpenTelemetry: the in-memory exporters collect into these lists while the providers' listeners
// are active, then Telemetry.Summarize prints a compact confirmation instead of dumping every span/metric.
List<Activity> spans = [];
List<Metric> metrics = [];
using var tracerProvider = Telemetry.CreateTracerProvider(spans);
using var meterProvider = Telemetry.CreateMeterProvider(metrics);
using var handler = Pipeline.Build(args);
var report = await handler.InvokeAsync(input);

if (report.ErrorMessage is { } error)
{
    await Console.Error.WriteLineAsync($"error: {error}");
    return 2;
}

Console.WriteLine($"original:   {report.Original}");
Console.WriteLine($"normalized: {report.Normalized}");
Console.WriteLine($"tokens:     [{string.Join(", ", report.Tokens)}]");
Console.WriteLine($"words:      {report.WordCount}");
Console.WriteLine($"elapsed:    {report.Elapsed.TotalMilliseconds:F2}ms");

// Flush the metrics so the in-memory exporter has them, then show what OpenTelemetry collected.
_ = meterProvider.ForceFlush();
Console.WriteLine(Telemetry.Summarize(spans, metrics));
return 0;
