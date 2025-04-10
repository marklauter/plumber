﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

internal sealed class RequestHandlerBuilder<TRequest, TResponse>
    : IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : notnull
{
    private const string DevEnv = "Development";

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP006:Implement IDisposable", Justification = "it's registered as singleton in Build()")]
    public IConfigurationManager Configuration { get; } = new ConfigurationManager();

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

    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    public IRequestHandler<TRequest, TResponse> Build() =>
        Build(Configuration.GetValue("RequestTimeout", Timeout.InfiniteTimeSpan));

    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    public IRequestHandler<TRequest, TResponse> Build(TimeSpan requestTimeout)
    {
        Services.TryAddSingleton<IConfiguration>(Configuration);

        return new RequestHandler<TRequest, TResponse>(
            Services,
            requestTimeout);
    }
}

