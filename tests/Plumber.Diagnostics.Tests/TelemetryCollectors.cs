using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Plumber.Diagnostics.Tests;

/// <summary>
/// A captured metric measurement: the instrument name, the recorded value (long counters are widened to
/// double for uniform assertions), and the tags attached to the measurement.
/// </summary>
internal sealed record CapturedMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

/// <summary>
/// Subscribes a BCL <see cref="ActivityListener"/> to a single <see cref="ActivitySource"/> by name and
/// collects every stopped activity, so tests can assert on real spans without the OpenTelemetry SDK.
/// </summary>
internal sealed class ActivityCollector : IDisposable
{
    private readonly ActivityListener listener;

    public Collection<Activity> Activities { get; } = [];

    public ActivityCollector(string sourceName)
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Activities.Add,
        };

        ActivitySource.AddActivityListener(listener);
    }

    public void Dispose() => listener.Dispose();
}

/// <summary>
/// Subscribes a BCL <see cref="MeterListener"/> to a single <see cref="Meter"/> by name and collects every
/// measurement, so tests can assert on real metrics without the OpenTelemetry SDK.
/// </summary>
internal sealed class MeterCollector : IDisposable
{
    private readonly MeterListener listener;

    public Collection<CapturedMeasurement> Measurements { get; } = [];

    public MeterCollector(string meterName)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) => Measurements.Add(new CapturedMeasurement(instrument.Name, measurement, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) => Measurements.Add(new CapturedMeasurement(instrument.Name, measurement, ToDictionary(tags))));

        listener.Start();
    }

    public void Dispose() => listener.Dispose();

    private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dictionary = new Dictionary<string, object?>(tags.Length);
        foreach (var tag in tags)
        {
            dictionary[tag.Key] = tag.Value;
        }

        return dictionary;
    }
}
