namespace Dialogue;

/// <summary>
/// Defines middleware that can be used in a request handler pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public interface IMiddleware<TRequest, TResponse>
    where TRequest : class
{
    /// <summary>
    /// The next delegate in the pipeline.
    /// </summary>
    Handler<string, string> Next { get; }

    /// <summary>
    /// Request handling method.
    /// Invoke Next to pass the context to the next middleware in the pipeline.
    /// </summary>
    /// <param name="context"><see cref="RequestContext{TRequest, TResponse}"/></param>
    /// <returns><see cref="Task"/></returns>
    Task InvokeAsync(RequestContext<TRequest, TResponse> context);
}

