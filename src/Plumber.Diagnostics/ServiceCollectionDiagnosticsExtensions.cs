using Microsoft.Extensions.DependencyInjection;

namespace Plumber.Diagnostics;

/// <summary>
/// ServiceCollectionDiagnosticsExtensions registers the options infrastructure the OpenTelemetry
/// tracing and metrics middleware resolve from the request handler's service provider.
/// </summary>
public static class ServiceCollectionDiagnosticsExtensions
{
    /// <summary>
    /// Registers the <see cref="RequestTracingOptions{TRequest, TResponse}"/> and
    /// <see cref="RequestMetricsOptions{TRequest, TResponse}"/> options infrastructure so the parameterless
    /// <c>UseRequestTracing</c>, <c>UseRequestMetrics</c>, and <c>UseRequestDiagnostics</c>
    /// overloads resolve their <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> dependency to the
    /// configured values (or the baked-in defaults).
    /// </summary>
    /// <typeparam name="TRequest">The pipeline request type.</typeparam>
    /// <typeparam name="TResponse">The pipeline response type.</typeparam>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configureTracing">Optional configuration for <see cref="RequestTracingOptions{TRequest, TResponse}"/>.</param>
    /// <param name="configureMetrics">Optional configuration for <see cref="RequestMetricsOptions{TRequest, TResponse}"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// This registers only Plumber's options; collecting the emitted telemetry is the consumer's job — register the
    /// OpenTelemetry SDK separately and subscribe to <see cref="PlumberDiagnostics.ActivitySourceName"/> and
    /// <see cref="PlumberDiagnostics.MeterName"/> via <c>AddSource</c> / <c>AddMeter</c>.
    /// </remarks>
    public static IServiceCollection AddPlumberDiagnostics<TRequest, TResponse>(
        this IServiceCollection services,
        Action<RequestTracingOptions<TRequest, TResponse>>? configureTracing = null,
        Action<RequestMetricsOptions<TRequest, TResponse>>? configureMetrics = null)
        where TRequest : notnull
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        var tracing = services.AddOptions<RequestTracingOptions<TRequest, TResponse>>();
        if (configureTracing is not null)
        {
            _ = tracing.Configure(configureTracing);
        }

        var metrics = services.AddOptions<RequestMetricsOptions<TRequest, TResponse>>();
        if (configureMetrics is not null)
        {
            _ = metrics.Configure(configureMetrics);
        }

        return services;
    }
}
