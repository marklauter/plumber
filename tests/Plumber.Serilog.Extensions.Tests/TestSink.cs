using Serilog.Core;
using Serilog.Events;
using System.Collections.ObjectModel;

namespace Plumber.Serilog.Extensions.Tests;

internal sealed class TestSink
    : ILogEventSink
{
    public Collection<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
