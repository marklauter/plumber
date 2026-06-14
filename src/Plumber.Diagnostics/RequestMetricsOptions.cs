namespace Plumber.Diagnostics;

/// <summary>
/// RequestMetricsOptions lets you configure the OpenTelemetry metrics middleware.
/// </summary>
/// <typeparam name="TRequest">The pipeline request type.</typeparam>
/// <typeparam name="TResponse">The pipeline response type.</typeparam>
public sealed class RequestMetricsOptions<TRequest, TResponse>
    where TRequest : class
{
    /// <summary>
    /// ThrowOnException lets you set whether the middleware should rethrow exceptions thrown by downstream middleware components.
    /// </summary>
    public bool ThrowOnException { get; set; } = true;

    /// <summary>
    /// AddDefaultMetrics lets you configure whether default metrics should be recorded.
    /// </summary>
    /// <remarks>
    /// Default metrics include request count, duration, and error count.
    /// </remarks>
    public bool AddDefaultMetrics { get; set; } = true;

    /// <summary>
    /// RecordCustomMetrics lets you provide an action to record custom metrics for each request.
    /// </summary>
    /// <remarks>
    /// The boolean parameter indicates whether the request was successful (true) or resulted in an error (false).
    /// </remarks>
    public Action<RequestContext<TRequest, TResponse>, bool>? RecordCustomMetrics { get; set; }
}
