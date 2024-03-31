using Microsoft.Extensions.Configuration;

namespace Dialogue;

/// <summary>
/// Extensions to create new typed request handler builders.
/// <seealso cref="IRequestHandlerBuilder{TRequest, TResponse}"/>
/// </summary>
public static class RequestHandlerBuilder
{
    /// <summary>
    /// Creates a new request handler builder.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>()
        where TRequest : class
        where TResponse : class => new RequestHandlerBuilder<TRequest, TResponse>([]);

    /// <summary>
    /// Creates a new request handler builder.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into Main(). Used to build <see cref="IConfiguration"/> with <see cref="IConfigurationBuilder"/>.AddCommandLine(args)</param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>(string[] args)
        where TRequest : class
        where TResponse : class => new RequestHandlerBuilder<TRequest, TResponse>(args);
}

