using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Dialogue;

/// <inheritdoc/>
internal sealed class RequestHandlerBuilder<TRequest, TResponse>
    : IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private IConfiguration configuration;
    private readonly ConfigurationBuilder configurationBuilder = new();
    private readonly IServiceCollection services = new ServiceCollection();

    internal RequestHandlerBuilder(string[] args)
    {
        configuration = configurationBuilder
            .AddCommandLine(args)
            .Build();
    }

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
        var requestTimeout = configuration.GetValue("RequestTimeout", Timeout.InfiniteTimeSpan);

#pragma warning disable IDISP004 // Don't ignore created IDisposable - service provider lifetime == lifetime of the application
        return new RequestHandler<TRequest, TResponse>(
            services.BuildServiceProvider(),
            requestTimeout);
#pragma warning restore IDISP004 // Don't ignore created IDisposable
    }
}

