namespace Plumber;

/// <summary>
/// A function for processing request context.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <param name="context">The request context flowing through the pipeline.</param>
/// <returns>A <see cref="Task"/> that completes when this middleware (and any downstream middleware it calls) finishes.</returns>
public delegate Task RequestMiddleware<TRequest, TResponse>(
    RequestContext<TRequest, TResponse> context)
    where TRequest : notnull;
