using Microsoft.Extensions.Options;

namespace Plumber.Diagnostics;

/// <summary>
/// RequestHandlerDiagnosticsExtensions provides extension methods for registering the OpenTelemetry
/// tracing and metrics middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
/// </summary>
public static class RequestHandlerDiagnosticsExtensions
{
    /// <summary>
    /// UseRequestTracing registers the tracing middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// Options are resolved from the request handler's service provider as <see cref="IOptions{TOptions}"/>; register
    /// them with <see cref="ServiceCollectionDiagnosticsExtensions.AddPlumberDiagnostics"/>, or omit registration
    /// to use the defaults baked into <see cref="RequestTracingOptions{TRequest, TResponse}"/>.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseRequestTracing<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler.Use<RequestTracingMiddleware<TRequest, TResponse>>();
    }

    /// <summary>
    /// UseRequestTracing registers the tracing middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>
    /// and applies the supplied inline configuration.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <param name="configureOptions">An action that configures <see cref="RequestTracingOptions{TRequest, TResponse}"/> for this registration.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// The configured options are passed directly to the middleware and take precedence over any registered in the service provider.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseRequestTracing<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler,
        Action<RequestTracingOptions<TRequest, TResponse>> configureOptions)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new RequestTracingOptions<TRequest, TResponse>();
        configureOptions(options);
        return handler.Use<RequestTracingMiddleware<TRequest, TResponse>>(Options.Create(options));
    }

    /// <summary>
    /// UseRequestMetrics registers the metrics middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// Options are resolved from the request handler's service provider as <see cref="IOptions{TOptions}"/>; register
    /// them with <see cref="ServiceCollectionDiagnosticsExtensions.AddPlumberDiagnostics"/>, or omit registration
    /// to use the defaults baked into <see cref="RequestMetricsOptions{TRequest, TResponse}"/>.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseRequestMetrics<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler.Use<RequestMetricsMiddleware<TRequest, TResponse>>();
    }

    /// <summary>
    /// UseRequestMetrics registers the metrics middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>
    /// and applies the supplied inline configuration.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <param name="configureOptions">An action that configures <see cref="RequestMetricsOptions{TRequest, TResponse}"/> for this registration.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <remarks>
    /// The configured options are passed directly to the middleware and take precedence over any registered in the service provider.
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> UseRequestMetrics<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler,
        Action<RequestMetricsOptions<TRequest, TResponse>> configureOptions)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new RequestMetricsOptions<TRequest, TResponse>();
        configureOptions(options);
        return handler.Use<RequestMetricsMiddleware<TRequest, TResponse>>(Options.Create(options));
    }

    /// <summary>
    /// UseRequestDiagnostics registers both the tracing and metrics middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    public static RequestHandler<TRequest, TResponse> UseRequestDiagnostics<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler
            .UseRequestTracing()
            .UseRequestMetrics();
    }

    /// <summary>
    /// UseRequestDiagnostics registers both the tracing and metrics middleware with the <see cref="RequestHandler{TRequest, TResponse}"/>
    /// and applies the supplied inline configuration to each.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="handler">The <see cref="RequestHandler{TRequest, TResponse}"/> to register the middleware with.</param>
    /// <param name="configureTracingOptions">An action that configures <see cref="RequestTracingOptions{TRequest, TResponse}"/> for the tracing middleware.</param>
    /// <param name="configureMetricsOptions">An action that configures <see cref="RequestMetricsOptions{TRequest, TResponse}"/> for the metrics middleware.</param>
    /// <returns>The same <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    public static RequestHandler<TRequest, TResponse> UseRequestDiagnostics<TRequest, TResponse>(
        this RequestHandler<TRequest, TResponse> handler,
        Action<RequestTracingOptions<TRequest, TResponse>> configureTracingOptions,
        Action<RequestMetricsOptions<TRequest, TResponse>> configureMetricsOptions)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(configureTracingOptions);
        ArgumentNullException.ThrowIfNull(configureMetricsOptions);
        return handler
            .UseRequestTracing(configureTracingOptions)
            .UseRequestMetrics(configureMetricsOptions);
    }
}
