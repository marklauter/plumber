﻿using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Plumber;

/// <summary>
/// Use IRequestHandler to setup and invoke the request/response pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    : IDisposable
    where TRequest : class
{
    /// <summary>
    /// Handler level service provider.
    /// </summary>
    ServiceProvider Services { get; }

    /// <summary>
    /// The timeout for the request handler's pipeline.
    /// </summary>
    /// <remarks>
    /// When Timeout is set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> <see cref="CancellationToken.None"/> is passed to the <see cref="RequestContext{TRequest, TResponse}"/> constructor.
    /// Otherwise, a timeout-based <see cref="CancellationTokenSource"/> is used to provide the cancelation token for the request context.
    /// </remarks>
    TimeSpan Timeout { get; }

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Task{TResponse}</returns>
    /// <remarks>
    /// InvokeAsync creates a new <see cref="RequestContext{TRequest, TResponse}"/> and passes it through the request handler's pipeline.
    /// The service provider passed to the RequestContext constructor is scoped to the request handler's ServiceProvider, and is disposed of after the request handler's pipeline completes.
    /// So there is no need for users of the RequestContext.Services property to call CreateScope().
    /// </remarks>
    Task<TResponse?> InvokeAsync(TRequest request);

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task{TResponse}</returns>
    /// <remarks>
    /// InvokeAsync creates a new <see cref="RequestContext{TRequest, TResponse}"/> and passes it through the request handler's pipeline.
    /// The service provider passed to the RequestContext constructor is scoped to the request handler's ServiceProvider, and is disposed of after the request handler's pipeline completes.
    /// So there is no need for users of the RequestContext.Services property to call CreateScope().
    /// </remarks>
    Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T, TResult}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/></param>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    IRequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T1, T2, TResult}"/>, <see cref="RequestContext{TRequest, TResponse}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/>, <see cref="Task"/></param>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class;

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">A class that contains an InvokeAsync method that receives a context.</typeparam>
    /// <param name="parameters">Contructor arguments for the middleware implementation.</param>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    /// <remarks>
    /// Constructor arguments are always passed after the Next middleware argument and before arguments provided by the service provider.
    /// </remarks>
    IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>(params object[] parameters)
        where TMiddleware : class;
}

