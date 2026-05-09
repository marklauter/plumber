using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

/// <summary>
/// A builder for request handlers.
/// Configure, register services for, and build <see cref="RequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public sealed class RequestHandlerBuilder<TRequest, TResponse>
    : ILoggingBuilder
    , IMetricsBuilder
    where TRequest : notnull
{
    private const string DevEnv = "Development";

    /// <summary>
    /// The <see cref="IConfigurationManager"/> for the request handler.
    /// </summary>
    /// <remarks><seealso href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0"/></remarks>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP006:Implement IDisposable",
        Justification = "ownership transfers to RequestHandler at Build()")]
    public IConfigurationManager Configuration { get; } = new ConfigurationManager();

    /// <summary>
    /// The <see cref="IServiceCollection"/> for the request handler.
    /// </summary>
    public IServiceCollection Services { get; } = new ServiceCollection();

    internal RequestHandlerBuilder(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? DevEnv;
        _ = Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("DOTNET_")
            .AddEnvironmentVariables()
            .AddUserSecrets(Assembly.GetExecutingAssembly(), true, true)
            .AddCommandLine(args);
    }

    internal RequestHandlerBuilder(string[] args, Action<IConfiguration, string[]> configure)
    {
        configure(Configuration, args);
    }

    /// <summary>
    /// Call Build to create an instance of <see cref="RequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    public RequestHandler<TRequest, TResponse> Build() =>
        Build(Configuration.GetValue("RequestTimeout", Timeout.InfiniteTimeSpan));

    /// <summary>
    /// Call Build to create an instance of <see cref="RequestHandler{TRequest, TResponse}"/> with a custom request timeout.
    /// </summary>
    /// <param name="requestTimeout">The timeout applied to each request invocation.</param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    public RequestHandler<TRequest, TResponse> Build(TimeSpan requestTimeout)
    {
        Services.TryAddSingleton<IConfiguration>(Configuration);

        return new RequestHandler<TRequest, TResponse>(
            Services,
            requestTimeout,
            (ConfigurationManager)Configuration);
    }
}
