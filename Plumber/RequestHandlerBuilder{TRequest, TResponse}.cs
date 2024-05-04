using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

/// <inheritdoc/>
internal sealed class RequestHandlerBuilder<TRequest, TResponse>
    : IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
{
    private const string DevEnv = "Development";

    /// <inheritdoc/>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP006:Implement IDisposable", Justification = "it's registered as singleton in Build()")]
    public IConfigurationManager Configuration { get; } = new ConfigurationManager();

    /// <inheritdoc/>
    public IServiceCollection Services { get; } = new ServiceCollection();

    internal RequestHandlerBuilder(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? DevEnv;
        _ = Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("DOTNET_")
            .AddEnvironmentVariables();

        if (environment.Equals(DevEnv, StringComparison.OrdinalIgnoreCase))
        {
            _ = Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), true, true);
        }

        _ = Configuration
            .AddCommandLine(args);

        _ = Services
            .AddLogging();
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    public IRequestHandler<TRequest, TResponse> Build()
    {
        var requestTimeout = Configuration.GetValue("RequestTimeout", Timeout.InfiniteTimeSpan);

        Services.TryAddSingleton<IConfiguration>(Configuration);

#pragma warning disable IDISP004 // Don't ignore created IDisposable - service provider lifetime == lifetime of the application
        return new RequestHandler<TRequest, TResponse>(
            Services.BuildServiceProvider(),
            requestTimeout);
#pragma warning restore IDISP004 // Don't ignore created IDisposable
    }

    public IRequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables() => throw new NotImplementedException();
}

