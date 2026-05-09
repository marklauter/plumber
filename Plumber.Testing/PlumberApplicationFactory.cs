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
public sealed class PlumberApplicationFactory<TRequest, TResponse> : IDisposable
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
    /// <remarks>WAF analog: <c>ConfigureTestServices</c>.</remarks>
    public PlumberApplicationFactory<TRequest, TResponse> WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureServices((_, services) => configure(services)));
    }

    /// <summary>
    /// Customize logging before the pipeline is built. Sugar over <see cref="WithBuilder"/>.
    /// </summary>
    public PlumberApplicationFactory<TRequest, TResponse> WithLogging(Action<ILoggingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureLogging((_, lb) => configure(lb)));
    }

    /// <summary>
    /// Customize configuration sources before the pipeline is built. Sugar over <see cref="WithBuilder"/>.
    /// </summary>
    public PlumberApplicationFactory<TRequest, TResponse> WithConfiguration(Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithBuilder(builder => builder.ConfigureConfiguration((cb, _) => configure(cb)));
    }

    /// <summary>
    /// Build (or return the cached) handler. Subsequent calls return the same instance.
    /// </summary>
    /// <remarks>WAF analog: <c>CreateClient</c>.</remarks>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "the RequestHandler returned by configurePipeline is the same instance assigned to the handler field")]
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

        handler = configurePipeline(builder.Build());
        return handler;
    }

    /// <summary>
    /// Convenience: invoke the pipeline with a single request.
    /// </summary>
    public Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken = default) =>
        CreateHandler().InvokeAsync(request, cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        handler?.Dispose();
        disposed = true;
    }
}
