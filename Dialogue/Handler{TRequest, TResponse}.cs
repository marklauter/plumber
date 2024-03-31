namespace Dialogue;

/// <summary>
/// Request handler delegate that can be used in a request handler pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
/// <param name="context"></param>
/// <returns></returns>
public delegate Task Handler<TRequest, TResponse>(
    Context<TRequest, TResponse> context)
    where TRequest : class
    where TResponse : class;
