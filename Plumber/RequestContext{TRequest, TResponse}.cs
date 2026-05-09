using System.Diagnostics.CodeAnalysis;

namespace Plumber;

/// <summary>
/// The request context holds the request, response, and other data that can be passed from one request delegate, or middleware, to another.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <param name="request">The request.</param>
/// <param name="id">Id for tracing the request. <see cref="Ulid"/></param>
/// <param name="timestamp">Request timestamp. <see cref="DateTime"/></param>
/// <param name="services"><see cref="IServiceProvider"/></param>
/// <param name="cancellationToken">Each delegate should call CancelationToken.ThrowIfCancellationRequested() before processing or forwarding the request context. <see cref="CancellationToken"/> </param>
public sealed class RequestContext<TRequest, TResponse>(
    TRequest request,
    Ulid id,
    DateTime timestamp,
    IServiceProvider services,
    CancellationToken cancellationToken)
    where TRequest : notnull
{
    private Dictionary<string, object?>? data;

    /// <summary>
    /// The request.
    /// </summary>
    public TRequest Request => request;

    /// <summary>
    /// Id for tracing the request.
    /// </summary>
    public Ulid Id => id;

    /// <summary>
    /// Request timestamp.
    /// </summary>
    public DateTime Timestamp => timestamp;

    /// <summary>
    /// The scoped <see cref="IServiceProvider"/> for the request.
    /// </summary>
    public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Cancellation token for the request.
    /// </summary>
    public CancellationToken CancellationToken => cancellationToken;

    /// <summary>
    /// Data that can be passed from one middleware to another.
    /// </summary>
    public IDictionary<string, object?> Data => data ??= [];

    /// <summary>
    /// Gets the value associated with the specified key, if it is of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value.</typeparam>
    /// <param name="key">The key of the element in the <see cref="Data"/> dictionary.</param>
    /// <param name="item">When this method returns <see langword="true"/>, contains the stored value cast to <typeparamref name="T"/>; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the key exists and the stored value is a non-null <typeparamref name="T"/>; otherwise <see langword="false"/>.</returns>
    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? item)
    {
        if (data is not null && data.TryGetValue(key, out var value) && value is T typed)
        {
            item = typed;
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    /// The response.
    /// </summary>
    public TResponse? Response { get; set; }

    /// <summary>
    /// Time since the request was created.
    /// </summary>
    public TimeSpan Elapsed => DateTime.UtcNow - timestamp;
}
