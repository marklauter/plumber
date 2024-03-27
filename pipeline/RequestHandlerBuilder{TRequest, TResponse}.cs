using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        this.Configuration = this.configurationBuilder.Build();
    }

    private readonly ConfigurationBuilder configurationBuilder = new();
    private readonly List<Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>>> components = [];

    public IServiceCollection Services { get; } = new ServiceCollection();

    public IConfiguration Configuration { get; private set; } = null!;

    public RequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables()
    {
        this.Configuration = this.configurationBuilder
            .AddEnvironmentVariables()
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(bool optional, bool reloadOnChange)
    {
        this.Configuration = this.configurationBuilder
            .AddUserSecrets(Assembly.GetExecutingAssembly(), optional, reloadOnChange)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(string userSecretId, bool reloadOnChange)
    {
        this.Configuration = this.configurationBuilder
            .AddUserSecrets(userSecretId, reloadOnChange)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddCommandLineArgs(string[] args)
    {
        this.Configuration = this.configurationBuilder
            .AddCommandLine(args)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange)
    {
        this.Configuration = this.configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange, string environment)
    {
        this.Configuration = this.configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> AddInMemoryCollection(IDictionary<string, string?> options)
    {
        this.Configuration = this.configurationBuilder
            .AddInMemoryCollection(options)
            .Build();

        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware)
    {
        this.components.Add(middleware);
        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>, Task> middleware)
    {
        return this.Use(next => context => middleware(context, next));
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>
    {
        // todo: item 2: once the item 1 todo is complete we can remove this line
        this.Services.TryAddSingleton<TMiddleware>();

        return this.Use(
            next =>
            async context =>
            {
                // todo: item 1: need to use service provider to resolve all middleware constructor args,
                // then use ActivatorUtilities.CreateInstance to create the middleware passing the Next delegate as first item or something like that. 
                // cause it's stupid to allow the Next delegate to be settable from outside the middleware.
                using var serviceScope = context.Services.CreateScope();
                var middleware = serviceScope.ServiceProvider.GetRequiredService<TMiddleware>();
                middleware.Next ??= next;

                await middleware.InvokeAsync(context);
            });
    }

    private RequestDelegate<TRequest, TResponse> BuildPipeline()
    {
        RequestDelegate<TRequest, TResponse> pipeline = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
                : Task.FromResult(context);

        for (var i = this.components.Count - 1; i >= 0; --i)
        {
            pipeline = this.components[i](pipeline);
        }

        return pipeline;
    }

    public RequestHandler<TRequest, TResponse> Build()
    {
        var requestTimeout = this.Configuration.GetValue("RequestTimeout", TimeSpan.FromMinutes(5));

#pragma warning disable IDISP004 // Don't ignore created IDisposable - RequestHandler lives for the duration of the application and so does service provider, so dispose is not required
        return new RequestHandler<TRequest, TResponse>(
            this.BuildPipeline(),
            this.Services.BuildServiceProvider(),
            requestTimeout);
#pragma warning restore IDISP004 // Don't ignore created IDisposable
    }
}

