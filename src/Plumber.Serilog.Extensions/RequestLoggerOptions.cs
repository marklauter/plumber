using Serilog;
using Serilog.Events;

namespace Plumber.Serilog.Extensions;

/// <summary>
/// RequestLoggerOptions lets you configure the Serilog middleware.
/// </summary>
/// <typeparam name="TRequest">The pipeline request type.</typeparam>
/// <typeparam name="TResponse">The pipeline response type.</typeparam>
public sealed class RequestLoggerOptions<TRequest, TResponse>
    where TRequest : class
{
    private const string DefaultCompletedMessage =
        "Request {RequestId} completed in {Elapsed:0.0000} ms";

    private static IEnumerable<LogEventProperty> DefaultGetMessageTemplateProperties(RequestContext<TRequest, TResponse> context) =>
    [
        // Surfaced as "RequestId" to bind the {RequestId} token in DefaultCompletedMessage; the value is RequestContext.Id.
        new LogEventProperty("RequestId", new ScalarValue(context.Id)),
        new LogEventProperty(nameof(RequestContext<,>.Elapsed), new ScalarValue(context.Elapsed.TotalMilliseconds))
    ];

    /// <summary>
    /// EnrichDiagnosticContext lets you provide an action within which you can enrich the diagnostic context with additional information per request.
    /// </summary>
    public Action<IDiagnosticContext, RequestContext<TRequest, TResponse>>? EnrichDiagnosticContext { get; set; }

    /// <summary>
    /// LogLevel lets you set the level for the Serilog middleware.
    /// </summary>
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// MessageTemplate lets you provide a custom message template.
    /// </summary>
    /// <remarks>
    /// <see cref="GetMessageTemplateProperties"/> must return the properties that are used in the message template, or the message template may not be rendered.
    /// </remarks>
    public string MessageTemplate { get; set; } = DefaultCompletedMessage;

    /// <summary>
    /// GetMessageTemplateProperties lets you provide a function that returns the properties that are used in the message template.
    /// </summary>
    /// <remarks>
    /// <see cref="MessageTemplate"/> must contain the properties returned by GetMessageTemplateProperties, or the message template may not be rendered.
    /// </remarks>
    public Func<RequestContext<TRequest, TResponse>, IEnumerable<LogEventProperty>> GetMessageTemplateProperties { get; set; } = DefaultGetMessageTemplateProperties;

    /// <summary>
    /// ThrowOnException lets you set whether the Serilog middleware should throw exceptions thrown by downstream middleware components.
    /// </summary>
    public bool ThrowOnException { get; set; } = true;
}
