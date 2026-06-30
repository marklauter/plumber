using Microsoft.Extensions.Options;

namespace Plumber.Serilog.Extensions;

/// <summary>
/// RequestHandlerSerilogExtensions provides extension methods for registering the Serilog middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
/// </summary>
public static class RequestHandlerSerilogExtensions
{
    /// <summary>
    /// UseSerilogRequestLogging registers the Serilog middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// Options are resolved from the request handler's service provider as <see cref="IOptions{TOptions}"/>; register
    /// them with <c>Configure&lt;RequestLoggerOptions&lt;TRequest, TResponse&gt;&gt;(...)</c>, or omit registration to use
    /// the defaults baked into <see cref="RequestLoggerOptions{TRequest, TResponse}"/>.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseSerilogRequestLogging<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler.Use<RequestLoggerMiddleware<TRequest, TResponse>>();
    }

    /// <summary>
    /// UseSerilogRequestLogging registers the Serilog middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>
    /// and applies the supplied inline configuration.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <param name="configureOptions">An action that configures <see cref="RequestLoggerOptions{TRequest, TResponse}"/> for this registration.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// The configured options are passed directly to the middleware and take precedence over any registered in the service provider.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseSerilogRequestLogging<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler,
        Action<RequestLoggerOptions<TRequest, TResponse>> configureOptions)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new RequestLoggerOptions<TRequest, TResponse>();
        configureOptions(options);
        return handler.Use<RequestLoggerMiddleware<TRequest, TResponse>>(Options.Create(options));
    }
}
