namespace Plumber;

/// <summary>
/// A function for processing request context.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <param name="context"></param>
/// <returns></returns>
public delegate Task RequestMiddleware<TRequest, TResponse>(
    RequestContext<TRequest, TResponse> context)
    where TRequest : class;
