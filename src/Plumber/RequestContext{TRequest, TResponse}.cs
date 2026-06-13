using System.Diagnostics.CodeAnalysis;

namespace Plumber;

/// <summary>
/// The request context holds the request, response, and other data that can be passed from one request delegate, or middleware, to another.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <remarks>
/// <see cref="RequestContext{TRequest, TResponse}"/> is not thread-safe. The pipeline invokes
/// middleware sequentially, so no synchronization is needed in normal use. Middleware that
/// fans out parallel work must not access the context (including <see cref="Data"/> and
/// <see cref="Response"/>) concurrently — complete context writes before forking, or
/// synchronize access and join before returning.
/// </remarks>
/// <param name="request">The request value flowed through the pipeline.</param>
/// <param name="id">A <see cref="Ulid"/> used to trace the request across logs and middleware.</param>
/// <param name="timeProvider">Time source used to capture <see cref="Timestamp"/> and measure <see cref="Elapsed"/>.</param>
/// <param name="services">Per-request scoped <see cref="IServiceProvider"/>; resolves services for middleware running in this invocation.</param>
/// <param name="cancellationToken">Cancellation signal for the request. Each delegate should call <c>CancellationToken.ThrowIfCancellationRequested()</c> before processing or forwarding the context.</param>
public sealed class RequestContext<TRequest, TResponse>(
    TRequest request,
    Ulid id,
    TimeProvider timeProvider,
    IServiceProvider services,
    CancellationToken cancellationToken)
    where TRequest : notnull
{
    // Stopwatch tick captured at construction so Elapsed uses the monotonic, high-resolution clock
    // rather than DateTime arithmetic (which has ~15.6ms resolution on Windows and is exposed to
    // wall-clock adjustments).
    private readonly long startTimestamp = timeProvider.GetTimestamp();
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
    /// Wall-clock timestamp captured when the context was created. Suitable for logging and correlation.
    /// </summary>
    public DateTime Timestamp { get; } = timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// The scoped <see cref="IServiceProvider"/> for the request.
    /// </summary>
    public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Cancellation token for the request.
    /// </summary>
    public CancellationToken CancellationToken => cancellationToken;

    /// <summary>
    /// Whether cancellation has been requested on the request's <see cref="CancellationToken"/>.
    /// Shorthand for <c>context.CancellationToken.IsCancellationRequested</c> — lets middleware short-circuit without throwing.
    /// </summary>
    public bool IsCanceled => cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Throws an <see cref="OperationCanceledException"/> if cancellation has been requested.
    /// Shorthand for <c>context.CancellationToken.ThrowIfCancellationRequested()</c>.
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancellation has been requested.</exception>
    public void ThrowIfCanceled() => cancellationToken.ThrowIfCancellationRequested();

    /// <summary>
    /// Data that can be passed from one middleware to another.
    /// </summary>
    /// <remarks>
    /// Created lazily on first access and not thread-safe — like the context itself.
    /// Parallel branches within a middleware must not read or write <see cref="Data"/> concurrently.
    /// </remarks>
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
    /// Time since the request was created, measured against the monotonic clock exposed by the configured <see cref="TimeProvider"/>.
    /// </summary>
    public TimeSpan Elapsed => timeProvider.GetElapsedTime(startTimestamp);
}
