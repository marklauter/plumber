namespace Plumber;

/// <summary>
/// The request context holds the request, response, and other data that can be passed from one request delegate, or middleware, to another.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <param name="Request">The request.</param>
/// <param name="Id">Id for tracing the request. <see cref="Ulid"/></param>
/// <param name="Timestamp">Request timestamp. <see cref="DateTime"/></param>
/// <param name="Services"><see cref="IServiceProvider"/></param>
/// <param name="CancellationToken">Each delegate should call CancelationToken.ThrowIfCancellationRequested() before processing or forwarding the request context. <see cref="CancellationToken"/> </param>
public record RequestContext<TRequest, TResponse>(
    TRequest Request,
    Ulid Id,
    DateTime Timestamp,
    IServiceProvider Services,
    CancellationToken CancellationToken)
    where TRequest : class
{
    /// <summary>
    /// Data that can be passed from one middleware to another.
    /// </summary>
    public IDictionary<string, object> Data { get; } = new Dictionary<string, object>();

    /// <summary>
    /// The response.
    /// </summary>
    public TResponse? Response { get; set; }

    /// <summary>
    /// Time since the request was created.
    /// </summary>
    public TimeSpan Elapsed => DateTime.UtcNow - Timestamp;
}
