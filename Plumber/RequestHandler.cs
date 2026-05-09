using Microsoft.Extensions.DependencyInjection;

namespace Plumber;

/// <summary>
/// Static factory for <see cref="RequestHandler{TRequest, TResponse}"/> instances that share an externally-owned <see cref="IServiceProvider"/>.
/// </summary>
/// <remarks>
/// Use these factories when hosting the pipeline inside an application whose <see cref="IServiceProvider"/> is built and owned elsewhere (for example, an ASP.NET Core host or a console app with its own DI root).
/// For the standalone case where the handler builds and owns its own provider, use <see cref="RequestHandlerBuilder.Create{TRequest, TResponse}()"/>.
/// </remarks>
public static class RequestHandler
{
    /// <summary>
    /// Creates a <see cref="RequestHandler{TRequest, TResponse}"/> backed by an externally-owned <see cref="IServiceProvider"/>, with no request timeout.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="services">A pre-built service provider. The handler does NOT take ownership and will not dispose it.</param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <remarks>
    /// <para><paramref name="services"/> must support <see cref="IServiceScopeFactory"/> (e.g. came from <see cref="ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(IServiceCollection)"/> or a host-built provider) because each invocation creates a new DI scope.</para>
    /// <para>If a <see cref="TimeProvider"/> is registered in <paramref name="services"/> it is used; otherwise <see cref="TimeProvider.System"/> is used.</para>
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> Create<TRequest, TResponse>(IServiceProvider services)
        where TRequest : notnull =>
        Create<TRequest, TResponse>(services, Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Creates a <see cref="RequestHandler{TRequest, TResponse}"/> backed by an externally-owned <see cref="IServiceProvider"/>, with a custom request timeout.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="services">A pre-built service provider. The handler does NOT take ownership and will not dispose it.</param>
    /// <param name="timeout">The timeout applied to each request invocation.</param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <remarks>
    /// <para><paramref name="services"/> must support <see cref="IServiceScopeFactory"/> (e.g. came from <see cref="ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(IServiceCollection)"/> or a host-built provider) because each invocation creates a new DI scope.</para>
    /// <para>If a <see cref="TimeProvider"/> is registered in <paramref name="services"/> it is used; otherwise <see cref="TimeProvider.System"/> is used.</para>
    /// </remarks>
    public static RequestHandler<TRequest, TResponse> Create<TRequest, TResponse>(IServiceProvider services, TimeSpan timeout)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        return new RequestHandler<TRequest, TResponse>(services, timeout);
    }
}
