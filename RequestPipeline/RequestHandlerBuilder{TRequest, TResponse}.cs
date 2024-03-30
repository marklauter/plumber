using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace RequestPipeline;

internal sealed class RequestHandlerBuilder<TRequest, TResponse>
    : IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    internal RequestHandlerBuilder(string[] args)
    {
        configuration = configurationBuilder
            .AddCommandLine(args)
            .Build();
    }

    private IConfiguration configuration;
    private readonly ConfigurationBuilder configurationBuilder = new();
    private readonly IServiceCollection services = new ServiceCollection();

    /// <inheritdoc/>
    public IRequestHandlerBuilder<TRequest, TResponse> Configure(Action<IConfigurationBuilder> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action.Invoke(configurationBuilder);
        configuration = configurationBuilder.Build();

        return this;
    }

    /// <inheritdoc/>
    public IRequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        action.Invoke(services, configuration);
        return this;
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    public IRequestHandler<TRequest, TResponse> Build()
    {
        var requestTimeout = configuration.GetValue("RequestTimeout", TimeSpan.FromMinutes(5));

#pragma warning disable IDISP004 // Don't ignore created IDisposable - service provider lives for the lifetime of the application
        return new RequestHandler<TRequest, TResponse>(
            services.BuildServiceProvider(),
            requestTimeout);
#pragma warning restore IDISP004 // Don't ignore created IDisposable
    }
}

