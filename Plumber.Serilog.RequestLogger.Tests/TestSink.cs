using Serilog.Core;
using Serilog.Events;

namespace Plumber.Serilog.Tests;

public class TestSink
    : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
