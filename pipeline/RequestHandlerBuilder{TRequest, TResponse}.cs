using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Pipeline.Tests;

public class RequestHandlerBuilder<TRequest, TResponse>
    : IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    public RequestHandlerBuilder()
    {
        this.configuration = this.configurationBuilder.Build();
    }

    private readonly ConfigurationBuilder configurationBuilder = new();
    private readonly List<Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>>> middleware = [];

    private readonly IServiceCollection services = new ServiceCollection();

    private IConfiguration configuration;

    public RequestHandlerBuilder<TRequest, TResponse> BuildConfiguration(Action<IConfigurationBuilder> action)
    {
        action?.Invoke(this.configurationBuilder);
        this.configuration = this.configurationBuilder.Build();
        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> action)
    {
        action?.Invoke(this.services, this.configuration);
        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables()
    {
        this.configuration = this.configurationBuilder
            .AddEnvironmentVariables()
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(bool optional, bool reloadOnChange)
    {
        this.configuration = this.configurationBuilder
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional, reloadOnChange)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(string userSecretId, bool reloadOnChange)
    {
        this.configuration = this.configurationBuilder
            .AddUserSecrets(userSecretId, reloadOnChange)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddCommandLineArgs(string[] args)
    {
        this.configuration = this.configurationBuilder
            .AddCommandLine(args)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange)
    {
        this.configuration = this.configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange, string environment)
    {
        this.configuration = this.configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddInMemoryCollection(IDictionary<string, string?> options)
    {
        this.configuration = this.configurationBuilder
            .AddInMemoryCollection(options)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware)
    {
        this.middleware.Add(middleware);
        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>, Task> middleware)
    {
        return this.Use(next => context => middleware(context, next));
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>
    {
        return this.Use(
            next =>
            async context =>
            {
                using var serviceScope = context.Services.CreateScope();
                var middleware = ActivatorUtilities
                    .CreateInstance<TMiddleware>(
                        serviceScope.ServiceProvider,
                        next);

                await middleware.InvokeAsync(context);
            });
    }

    private RequestDelegate<TRequest, TResponse> BuildPipeline()
    {
        RequestDelegate<TRequest, TResponse> pipeline = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
                : Task.FromResult(context);

        for (var i = this.middleware.Count - 1; i >= 0; --i)
        {
            pipeline = this.middleware[i](pipeline);
        }

        return pipeline;
    }

    public RequestHandler<TRequest, TResponse> Build()
    {
        var requestTimeout = this.configuration.GetValue("RequestTimeout", TimeSpan.FromMinutes(5));

#pragma warning disable IDISP004 // Don't ignore created IDisposable - RequestHandler lives for the duration of the application and so does service provider, so dispose is not required
        return new RequestHandler<TRequest, TResponse>(
            this.BuildPipeline(),
            this.services.BuildServiceProvider(),
            requestTimeout);
#pragma warning restore IDISP004 // Don't ignore created IDisposable
    }
}

