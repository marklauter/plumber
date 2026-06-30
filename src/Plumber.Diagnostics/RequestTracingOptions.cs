using System.Diagnostics;

namespace Plumber.Diagnostics;

/// <summary>
/// RequestTracingOptions lets you configure the OpenTelemetry tracing middleware.
/// </summary>
/// <typeparam name="TRequest">The pipeline request type.</typeparam>
/// <typeparam name="TResponse">The pipeline response type.</typeparam>
public sealed class RequestTracingOptions<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    private const string DefaultOperationName = "Plumber.HandleRequest";

    /// <summary>
    /// OperationName lets you configure the name of the operation/span.
    /// </summary>
    public string OperationName { get; set; } = DefaultOperationName;

    /// <summary>
    /// EnrichSpan lets you provide an action to enrich the span with additional information per request.
    /// </summary>
    public Action<Activity, RequestContext<TRequest, TResponse>>? EnrichSpan { get; set; }

    /// <summary>
    /// RecordException lets you configure whether exceptions should be recorded on the span.
    /// </summary>
    public bool RecordException { get; set; } = true;

    /// <summary>
    /// ThrowOnException lets you set whether the middleware should rethrow exceptions thrown by downstream middleware components.
    /// </summary>
    public bool ThrowOnException { get; set; } = true;

    /// <summary>
    /// SpanKind lets you set the kind of span to be created.
    /// </summary>
    public ActivityKind SpanKind { get; set; } = ActivityKind.Internal;

    /// <summary>
    /// AddDefaultAttributes lets you configure whether default attributes should be added to the span.
    /// </summary>
    /// <remarks>
    /// Default attributes include request ID, request type, elapsed time, and response type.
    /// </remarks>
    public bool AddDefaultAttributes { get; set; } = true;
}
