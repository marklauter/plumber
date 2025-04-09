using System.Diagnostics.CodeAnalysis;

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
    where TRequest : notnull
{

    private Dictionary<string, object?>? data;

    /// <summary>
    /// Data that can be passed from one middleware to another.
    /// </summary>
    public IDictionary<string, object?> Data => data ??= [];

    /// <summary>
    /// Returns the data associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The data type the to which the result will be cast.</typeparam>
    /// <param name="key">The key of the element in the Data dictionary.</param>
    /// <param name="item"></param>
    /// <returns>TData</returns>
    /// <remarks>If nothing has been added to the dicionary, then TryGetValue returns default(TData)</remarks>
    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? item) =>
        data?.TryGetValue(key, out var value) == true && (item = (T?)value) != null || (item = default) != null;

    /// <summary>
    /// The response.
    /// </summary>
    public TResponse? Response { get; set; }

    /// <summary>
    /// Time since the request was created.
    /// </summary>
    public TimeSpan Elapsed => DateTime.UtcNow - Timestamp;
}
