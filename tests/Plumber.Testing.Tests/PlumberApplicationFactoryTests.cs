using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Testing.Tests;

[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
    Justification = "the RequestHandler returned by CreateHandler is owned by the factory; the using-scoped factory disposes it")]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001:Dispose created",
    Justification = "the RequestHandler returned by CreateHandler is owned by the factory; the using-scoped factory disposes it")]
public sealed class PlumberApplicationFactoryTests
{
    private static RequestHandlerBuilder<string, string> CreateBuilder(string[] args) =>
        RequestHandlerBuilder.Create<string, string>(args);

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent .Use() returns the same handler instance; the factory owns it")]
    private static RequestHandler<string, string> ConfigurePipeline(RequestHandler<string, string> handler) =>
        handler.Use((context, next) =>
        {
            context.Response = context.Request.ToUpperInvariant();
            return next(context);
        });

    private static PlumberApplicationFactory<string, string> CreateFactory() =>
        new(CreateBuilder, ConfigurePipeline);

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "the constructor throws before anything disposable is created")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP005:Return type should indicate that the value should be disposed",
        Justification = "the constructor throws; no instance is ever created")]
    public void CtorThrowsOnNullArguments()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new PlumberApplicationFactory<string, string>(null!, ConfigurePipeline));
        _ = Assert.Throws<ArgumentNullException>(
            () => new PlumberApplicationFactory<string, string>(CreateBuilder, null!));
    }

    [Fact]
    public async Task InvokeAsyncRunsPipelineEndToEndAsync()
    {
        using var factory = CreateFactory();

        var response = await factory.InvokeAsync("hello", TestContext.Current.CancellationToken);

        Assert.Equal("HELLO", response);
    }

    [Fact]
    public void CommandLineArgsFlowIntoConfiguration()
    {
        using var factory = new PlumberApplicationFactory<string, string>(
            CreateBuilder,
            ConfigurePipeline,
            ["--Key=FromArgs"]);

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("FromArgs", configuration["Key"]);
    }

    [Fact]
    public void WithBuilderAppliesHookBeforeBuild()
    {
        var hookInvoked = false;

        using var factory = CreateFactory();
        _ = factory.WithBuilder(_ => hookInvoked = true);
        _ = factory.CreateHandler();

        Assert.True(hookInvoked);
    }

    [Fact]
    public void WithServicesRegistersOverride()
    {
        using var factory = CreateFactory();
        _ = factory.WithServices(services => services.AddSingleton(new ConfigProbe("override")));

        Assert.Equal("override", factory.Services.GetRequiredService<ConfigProbe>().Value);
    }

    [Fact]
    public void WithServicesReceivesBuiltConfiguration()
    {
        using var factory = CreateFactory();
        _ = factory
            .WithInMemorySettings([new("Probe:Value", "from-config")])
            .WithServices((services, configuration) =>
                services.AddSingleton(new ConfigProbe(configuration["Probe:Value"])));

        Assert.Equal("from-config", factory.Services.GetRequiredService<ConfigProbe>().Value);
    }

    [Fact]
    public void WithLoggingAppliesHookBeforeBuild()
    {
        var hookInvoked = false;

        using var factory = CreateFactory();
        _ = factory.WithLogging(_ => hookInvoked = true);
        _ = factory.CreateHandler();

        Assert.True(hookInvoked);
    }

    [Fact]
    public void WithConfigurationAddsSource()
    {
        using var factory = CreateFactory();
        _ = factory.WithConfiguration(config =>
            config.AddInMemoryCollection([new KeyValuePair<string, string?>("Key", "FromHook")]));

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("FromHook", configuration["Key"]);
    }

    [Fact]
    public void WithInMemorySettingsSeedsConfiguration()
    {
        using var factory = CreateFactory();
        _ = factory.WithInMemorySettings([new("Key", "Seeded")]);

        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("Seeded", configuration["Key"]);
    }

    [Fact]
    public void HookArgumentsAreNullGuarded()
    {
        using var factory = CreateFactory();

        _ = Assert.Throws<ArgumentNullException>(() => factory.WithBuilder(null!));
        _ = Assert.Throws<ArgumentNullException>(() => factory.WithServices((Action<IServiceCollection>)null!));
        _ = Assert.Throws<ArgumentNullException>(() => factory.WithServices((Action<IServiceCollection, IConfiguration>)null!));
        _ = Assert.Throws<ArgumentNullException>(() => factory.WithLogging(null!));
        _ = Assert.Throws<ArgumentNullException>(() => factory.WithConfiguration(null!));
        _ = Assert.Throws<ArgumentNullException>(() => factory.WithInMemorySettings(null!));
    }

    [Fact]
    public void CreateHandlerReturnsCachedInstance()
    {
        using var factory = CreateFactory();

        Assert.Same(factory.CreateHandler(), factory.CreateHandler());
    }

    [Fact]
    public void HooksAfterCreateHandlerThrow()
    {
        using var factory = CreateFactory();
        _ = factory.CreateHandler();

        var ex = Assert.Throws<InvalidOperationException>(() => factory.WithServices(_ => { }));

        Assert.Contains("after the handler has been created", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateHandlerDisposesBuiltHandlerWhenConfigurePipelineThrows()
    {
        RequestHandler<string, string>? leaked = null;
        using var factory = new PlumberApplicationFactory<string, string>(
            CreateBuilder,
            handler =>
            {
                leaked = handler;
                throw new InvalidOperationException("boom");
            });

        _ = Assert.Throws<InvalidOperationException>(factory.CreateHandler);

        Assert.NotNull(leaked);
        _ = Assert.Throws<ObjectDisposedException>(() => leaked.Use(next => next));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "the foreign handler is owned by the using-scoped local; the factory rejects it without taking ownership")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP011:Don't return disposed instance",
        Justification = "deliberately returning the using-scoped foreign handler to prove the factory rejects a non-identity result")]
    public void CreateHandlerThrowsWhenConfigurePipelineReturnsForeignHandler()
    {
        using var foreign = RequestHandlerBuilder.Create<string, string>().Build();
        using var factory = new PlumberApplicationFactory<string, string>(
            CreateBuilder,
            _ => foreign);

        var ex = Assert.Throws<InvalidOperationException>(factory.CreateHandler);

        Assert.Contains("must return the handler instance it was given", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateHandlerCachesBuildFailure()
    {
        var builds = 0;
        using var factory = new PlumberApplicationFactory<string, string>(
            args =>
            {
                ++builds;
                return CreateBuilder(args);
            },
            _ => throw new InvalidOperationException("boom"));

        _ = Assert.Throws<InvalidOperationException>(factory.CreateHandler);
        _ = Assert.Throws<InvalidOperationException>(factory.CreateHandler);

        Assert.Equal(1, builds);
    }

    [Fact]
    public void ServicesReturnsSameProviderAndFreezesHooks()
    {
        using var factory = CreateFactory();

        Assert.Same(factory.Services, factory.Services);
        _ = Assert.Throws<InvalidOperationException>(() => factory.WithServices(_ => { }));
    }

    [Fact]
    public void ServicesCreatesScopesForScopedServices()
    {
        using var factory = CreateFactory();
        _ = factory.WithServices(services => services.AddScoped<ScopedProbe>());

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        Assert.NotSame(
            scope1.ServiceProvider.GetRequiredService<ScopedProbe>(),
            scope2.ServiceProvider.GetRequiredService<ScopedProbe>());
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance",
        Justification = "the test exists specifically to verify behavior after dispose")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using",
        Justification = "the test needs to dispose mid-method to verify post-dispose behavior")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP005:Return type should indicate that the value should be disposed",
        Justification = "the factory is disposed before the lambda runs")]
    public void OperationsAfterDisposeThrow()
    {
        var factory = CreateFactory();
        factory.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(factory.CreateHandler);
        _ = Assert.Throws<ObjectDisposedException>(() => factory.WithServices(_ => { }));
        _ = Assert.Throws<ObjectDisposedException>(() => _ = factory.Services);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance",
        Justification = "test verifies double-dispose is safe")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using",
        Justification = "test disposes explicitly to assert idempotency")]
    public void DoubleDisposeIsSafe()
    {
        var factory = CreateFactory();
        factory.Dispose();
        factory.Dispose();
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance",
        Justification = "the test exists specifically to verify behavior after dispose")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using",
        Justification = "the test needs to dispose mid-method to verify post-dispose behavior")]
    public async Task DisposeAsyncDisposesHandlerAsync()
    {
        var factory = CreateFactory();
        var handler = factory.CreateHandler();

        await factory.DisposeAsync();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => handler.InvokeAsync("x", TestContext.Current.CancellationToken));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance",
        Justification = "test verifies double-dispose is safe")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using",
        Justification = "test disposes explicitly to assert idempotency")]
    public async Task DoubleDisposeAsyncWithoutHandlerIsSafeAsync()
    {
        var factory = CreateFactory();
        await factory.DisposeAsync();
        await factory.DisposeAsync();
    }

    private sealed class ScopedProbe;

    private sealed record ConfigProbe(string? Value);
}
