using Plumber;
using Plumber.Testing;
using System.Diagnostics.CodeAnalysis;

namespace Sample.Cli.Tests;

[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
    Justification = "the RequestHandler returned by CreateHandler is owned by the factory; the using-scoped factory disposes it")]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP001:Dispose created",
    Justification = "the RequestHandler returned by CreateHandler is owned by the factory; the using-scoped factory disposes it")]
public sealed class FactoryTests
{
    private static PlumberApplicationFactory<string, TextReport> CreateFactory() =>
        new(Pipeline.CreateBuilder, Pipeline.Configure);

    [Fact]
    public async Task FactoryInvokesPipelineEndToEndAsync()
    {
        using var factory = CreateFactory();

        var report = await factory.InvokeAsync("Hello, World! FOO", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Null(report.ErrorMessage);
        Assert.Equal("hello, world! foo", report.Normalized);
        Assert.Equal(3, report.WordCount);
    }

    [Fact]
    public void WithServicesAppliesHookBeforeBuild()
    {
        var hookInvoked = false;

        using var factory = CreateFactory();
        _ = factory.WithServices(_ => hookInvoked = true);
        _ = factory.CreateHandler();

        Assert.True(hookInvoked);
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
    public void WithLoggingAppliesHookBeforeBuild()
    {
        var hookInvoked = false;

        using var factory = CreateFactory();
        _ = factory.WithLogging(_ => hookInvoked = true);
        _ = factory.CreateHandler();

        Assert.True(hookInvoked);
    }

    [Fact]
    public void WithConfigurationAppliesHookBeforeBuild()
    {
        var hookInvoked = false;

        using var factory = CreateFactory();
        _ = factory.WithConfiguration(_ => hookInvoked = true);
        _ = factory.CreateHandler();

        Assert.True(hookInvoked);
    }

    [Fact]
    public void WithBuilderAfterCreateHandlerThrows()
    {
        using var factory = CreateFactory();
        _ = factory.CreateHandler();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            factory.WithServices(_ => { }));

        Assert.Contains("after the handler has been created", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateHandlerReturnsCachedInstance()
    {
        using var factory = CreateFactory();

        var first = factory.CreateHandler();
        var second = factory.CreateHandler();

        Assert.Same(first, second);
    }

    [Fact]
    public void CreateHandlerDisposesBuiltHandlerWhenConfigurePipelineThrows()
    {
        RequestHandler<string, TextReport>? leaked = null;
        using var factory = new PlumberApplicationFactory<string, TextReport>(
            Pipeline.CreateBuilder,
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
}
