using Microsoft.Extensions.Configuration;

namespace Plumber;

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
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>([]);

    /// <summary>
    /// Creates a new request handler builder and builds the default configurtion:
    /// using current directory
    ///     - AddJsonFile("appsettings.json", optional: true)
    ///     - AddJsonFile($"appsettings.{ENV}.json", optional: true)
    ///     - AddEnvironmentVariables("DOTNET_")
    ///     - AddEnvironmentVariables
    ///     - if ENV == DEV then AddUserSecrets 
    ///     - AddCommandLine(args)
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into Main(). Used to build <see cref="IConfiguration"/> with <see cref="IConfigurationBuilder"/>.AddCommandLine(args)</param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>(string[] args)
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>(args);

    /// <summary>
    /// Creates a new request handler builder, does NOT build a default configuration, and allows for custom configuration through the <paramref name="configure"/> action.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into Main(). Used to build <see cref="IConfiguration"/> with <see cref="IConfigurationBuilder"/>.AddCommandLine(args)</param>
    /// <param name="configure"></param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>(string[] args, Action<IConfiguration, string[]> configure)
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>(args, configure);

    /// <summary>
    /// Creates a new request handler builder.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> Create<TRequest, TResponse>()
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>([]);

    /// <summary>
    /// Creates a new request handler builder.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into Main(). Used to build <see cref="IConfiguration"/> with <see cref="IConfigurationBuilder"/>.AddCommandLine(args)</param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> Create<TRequest, TResponse>(string[] args)
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>(args);

    /// <summary>
    /// Creates a new request handler builder, does NOT build a default configuration, and allows for custom configuration through the <paramref name="configure"/> action.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into Main(). Used to build <see cref="IConfiguration"/> with <see cref="IConfigurationBuilder"/>.AddCommandLine(args)</param>
    /// <param name="configure"></param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    public static IRequestHandlerBuilder<TRequest, TResponse> Create<TRequest, TResponse>(string[] args, Action<IConfiguration, string[]> configure)
        where TRequest : class => new RequestHandlerBuilder<TRequest, TResponse>(args, configure);
}

