using System.Diagnostics.CodeAnalysis;

namespace Dialogue;

/// <summary>
/// An interface that defines the mechanisms to setup a request handler's pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : class
{
    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<TResponse?> InvokeAsync(TRequest request);

    /// <summary>
    /// Prepare's the request handler's internal middleware pipeline.
    /// </summary>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    IRequestHandler<TRequest, TResponse> Prepare();

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T, TResult}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/></param>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    IRequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T1, T2, TResult}"/>, <see cref="RequestContext{TRequest, TResponse}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/>, <see cref="Task"/></param>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware"><see cref="IMiddleware{TRequest, TResponse}"/></typeparam>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>;
}

