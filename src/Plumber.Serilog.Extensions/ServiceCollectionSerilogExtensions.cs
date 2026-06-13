using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Plumber.Serilog.Extensions;

/// <summary>
/// ServiceCollectionSerilogExtensions registers the services the Serilog request-logging middleware depends on.
/// </summary>
public static class ServiceCollectionSerilogExtensions
{
    /// <summary>
    /// Registers Serilog (the request <see cref="ILogger"/> and <c>DiagnosticContext</c>) and the
    /// <see cref="RequestLoggerOptions{TRequest, TResponse}"/> options infrastructure that the Serilog
    /// request-logging middleware resolves from the request handler's service provider.
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configureLogger">Configures the Serilog logger the middleware writes to.</param>
    /// <param name="configureOptions">Optional configuration for <see cref="RequestLoggerOptions{TRequest, TResponse}"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// Calling this makes the parameterless <see cref="RequestHandlerSerilogExtensions.UseSerilogRequestLogging{TRequest, TResponse}(RequestHandler{TRequest, TResponse})"/>
    /// overload self-sufficient: <c>AddOptions</c> guarantees the middleware's <c>IOptions</c> dependency resolves
    /// to the defaults baked into <see cref="RequestLoggerOptions{TRequest, TResponse}"/> even when no other
    /// component registered the options infrastructure.
    /// </remarks>
    public static IServiceCollection AddSerilogRequestLogging<TRequest, TResponse>(
        this IServiceCollection services,
        Action<LoggerConfiguration> configureLogger,
        Action<RequestLoggerOptions<TRequest, TResponse>>? configureOptions = null)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureLogger);

        _ = services.AddSerilog(configureLogger);
        var options = services.AddOptions<RequestLoggerOptions<TRequest, TResponse>>();
        if (configureOptions is not null)
        {
            _ = options.Configure(configureOptions);
        }

        return services;
    }
}
