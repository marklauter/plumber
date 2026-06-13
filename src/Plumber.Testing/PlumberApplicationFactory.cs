using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Testing;

/// <summary>
/// Factory for testing Plumber pipelines in-process. Models the
/// <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> pattern: bootstraps the application's real
/// builder and pipeline configuration, but lets tests override services and other builder
/// state before the pipeline is built.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public sealed class PlumberApplicationFactory<TRequest, TResponse> : IDisposable, IAsyncDisposable
    where TRequest : notnull
{
    private readonly string[] args;
    private readonly Func<string[], RequestHandlerBuilder<TRequest, TResponse>> createBuilder;
    private readonly Func<RequestHandler<TRequest, TResponse>, RequestHandler<TRequest, TResponse>> configurePipeline;
    private readonly List<Action<RequestHandlerBuilder<TRequest, TResponse>>> builderHooks = [];
    private RequestHandler<TRequest, TResponse>? handler;
    private bool disposed;

    /// <summary>
    /// Constructs a factory wrapping the application's pipeline.
    /// </summary>
    /// <param name="createBuilder">Returns the un-built builder. Typically the application's <c>Pipeline.CreateBuilder</c>.</param>
    /// <param name="configurePipeline">Adds middleware to a built handler. Typically the application's <c>Pipeline.Configure</c>.</param>
    /// <param name="args">Command-line args forwarded to the builder. Defaults to empty.</param>
    public PlumberApplicationFactory(
        Func<string[], RequestHandlerBuilder<TRequest, TResponse>> createBuilder,
        Func<RequestHandler<TRequest, TResponse>, RequestHandler<TRequest, TResponse>> configurePipeline,
        string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(createBuilder);
        ArgumentNullException.ThrowIfNull(configurePipeline);

        this.createBuilder = createBuilder;
        this.configurePipeline = configurePipeline;
        this.args = args ?? [];
    }

    /// <summary>
    /// Customize the builder before <c>Build()</c> is called. Multiple hooks compose in registration order.
    /// </summary>
    /// <param name="configure">Callback that mutates the <see cref="RequestHandlerBuilder{TRequest, TResponse}"/> before the handler is built.</param>
    /// <returns>This factory for chaining.</returns>
    /// <remarks>WAF analog: <c>WithWebHostBuilder</c>.</remarks>
    public PlumberApplicationFactory<TRequest, TResponse> WithBuilder(
        Action<RequestHandlerBuilder<TRequest, TResponse>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handler is not null)
        {
            throw new InvalidOperationException(
                "cannot configure builder after the handler has been created.");
        }

        builderHooks.Add(configure);
        return this;
    }

    /// <summary>
    /// Customize service registrations before the pipeline is built. Sugar for the most common case.
    /// </summary>
    /// <param name="configure">Callback that mutates the <see cref="IServiceCollection"/> before the handler is built.</param>
    /// <returns>This factory for chaining.</returns>
    /// <remarks>WAF analog: <c>ConfigureTestServices</c>.</remarks>
    public PlumberApplicationFactory<TRequest, TResponse> WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureServices((services, _) => configure(services)));
    }

    /// <summary>
    /// Customize service registrations before the pipeline is built, with the built <see cref="IConfiguration"/> available.
    /// </summary>
    /// <param name="configure">Callback that receives the <see cref="IServiceCollection"/> and the built <see cref="IConfiguration"/>.</param>
    /// <returns>This factory for chaining.</returns>
    /// <remarks>WAF analog: <c>ConfigureTestServices</c>.</remarks>
    public PlumberApplicationFactory<TRequest, TResponse> WithServices(Action<IServiceCollection, IConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureServices(configure));
    }

    /// <summary>
    /// Customize logging before the pipeline is built. Sugar over <see cref="WithBuilder"/>.
    /// </summary>
    /// <param name="configure">Callback that mutates the <see cref="ILoggingBuilder"/> before the handler is built.</param>
    /// <returns>This factory for chaining.</returns>
    public PlumberApplicationFactory<TRequest, TResponse> WithLogging(Action<ILoggingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureLogging(configure));
    }

    /// <summary>
    /// Customize configuration sources before the pipeline is built. Sugar over <see cref="WithBuilder"/>.
    /// </summary>
    /// <param name="configure">Callback that mutates the <see cref="IConfigurationBuilder"/> before the handler is built.</param>
    /// <returns>This factory for chaining.</returns>
    public PlumberApplicationFactory<TRequest, TResponse> WithConfiguration(Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureConfiguration((cb, _) => configure(cb)));
    }

    /// <summary>
    /// Seeds the configuration with an in-memory key/value collection. Common test-setup pattern for stubbing settings.
    /// </summary>
    /// <param name="settings">Key/value pairs added as a configuration source.</param>
    /// <returns>This factory for chaining.</returns>
    public PlumberApplicationFactory<TRequest, TResponse> WithInMemorySettings(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return WithBuilder(builder => builder.AddInMemoryCollection(settings));
    }

    /// <summary>
    /// Build (or return the cached) handler. Subsequent calls return the same instance.
    /// </summary>
    /// <returns>The built <see cref="RequestHandler{TRequest, TResponse}"/>; the same instance on every call until the factory is disposed.</returns>
    /// <remarks>WAF analog: <c>CreateClient</c>.</remarks>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "the RequestHandler returned by configurePipeline is the same instance assigned to the handler field")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001:Dispose created",
        Justification = "ownership of 'built' transfers to the handler field on success; the catch disposes it on failure")]
    public RequestHandler<TRequest, TResponse> CreateHandler()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handler is not null)
        {
            return handler;
        }

        var builder = createBuilder(args);
        foreach (var hook in builderHooks)
        {
            hook(builder);
        }

        var built = builder.Build();
        try
        {
            handler = configurePipeline(built);
        }
        catch
        {
            built.Dispose();
            throw;
        }

        return handler;
    }

    /// <summary>
    /// The root <see cref="IServiceProvider"/> of the built pipeline. Accessing it builds the handler,
    /// freezing the builder hooks, exactly as <see cref="CreateHandler"/> does.
    /// </summary>
    /// <remarks>
    /// Resolve singletons directly; create a scope via
    /// <see cref="ServiceProviderServiceExtensions.CreateScope(IServiceProvider)"/> to resolve scoped
    /// services (for example, a <c>DbContext</c>) for post-invocation assertions — resolving scoped
    /// services from the root provider produces captive dependencies.
    /// WAF analog: <c>Services</c>.
    /// </remarks>
    public IServiceProvider Services => CreateHandler().Services;

    /// <summary>
    /// Convenience: invoke the pipeline with a single request.
    /// </summary>
    /// <param name="request">The request value flowed through the pipeline.</param>
    /// <param name="cancellationToken">Caller-supplied cancellation token forwarded to <see cref="RequestHandler{TRequest, TResponse}.InvokeAsync(TRequest, CancellationToken)"/>.</param>
    /// <returns>A task that completes with the pipeline's response, or <see langword="null"/> if no middleware assigned <c>Response</c>.</returns>
    public Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken = default) =>
        CreateHandler().InvokeAsync(request, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Prefer <see cref="DisposeAsync"/> when any registered singleton implements only <see cref="IAsyncDisposable"/> —
    /// see <see cref="RequestHandler{TRequest, TResponse}.DisposeAsync"/>.
    /// </remarks>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        handler?.Dispose();
        disposed = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Disposes the built handler via <see cref="RequestHandler{TRequest, TResponse}.DisposeAsync"/> so
    /// singletons implementing only <see cref="IAsyncDisposable"/> are disposed correctly.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        if (handler is not null)
        {
            await handler.DisposeAsync();
        }

        disposed = true;
    }
}
