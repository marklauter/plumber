using Sample.Cli;

var input = args.Length > 0
    ? string.Join(' ', args)
    : await Console.In.ReadToEndAsync();

// Host-free OpenTelemetry: the providers' listeners must be active before the pipeline emits; dispose flushes the console exporter.
using var tracerProvider = Telemetry.CreateTracerProvider();
using var meterProvider = Telemetry.CreateMeterProvider();
using var handler = Pipeline.Build(args);
var report = await handler.InvokeAsync(input);

if (report is null)
{
    await Console.Error.WriteLineAsync("pipeline returned no response");
    return 1;
}

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
return 0;
