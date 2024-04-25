using Dialogue;
using Serilog;
using Serilog.Events;

namespace Dialog.Serilog.RequestLogger;

public sealed class RequestLoggerOptions<TRequest, TResponse>
    where TRequest : class
{
    private const string DefaultCompletedMessage = "Request {Id} completed in {Elapsed:0.0000} ms";

    private static IEnumerable<LogEventProperty> DefaultGetMessageTemplateProperties(RequestContext<TRequest, TResponse> context) =>
    [
        new LogEventProperty("Id", new ScalarValue(context.Id)),
        new LogEventProperty("Elapsed", new ScalarValue(context.Elapsed))
    ];

    public Action<IDiagnosticContext, RequestContext<TRequest, TResponse>>? EnrichDiagnosticContext { get; set; }
    public ILogger? Logger { get; set; }
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

    public string MessageTemplate { get; set; } = DefaultCompletedMessage;
    public Func<RequestContext<TRequest, TResponse>, IEnumerable<LogEventProperty>> GetMessageTemplateProperties { get; set; } = DefaultGetMessageTemplateProperties;

    public bool ThrowOnException { get; set; } = true;
}
