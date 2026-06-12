using Plumber.Tests.Middleware;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests;

public sealed class MiddlewareIntrospectionTests
{
    [Fact]
    public void MiddlewareIsEmptyBeforeAnyRegistration()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        Assert.Empty(handler.Middleware);
    }

    [Fact]
    public void UseClassMiddlewareRecordsMiddlewareTypeAndDisplayName()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<ToLowerMiddleware>();

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Equal(typeof(ToLowerMiddleware), descriptor.MiddlewareType);
        Assert.Equal(nameof(ToLowerMiddleware), descriptor.DisplayName);
    }

    [Fact]
    public void UseClassMiddlewareWithCtorParametersRecordsMiddlewareType()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<ToLowerMiddlewareWithParameter>("prefix");

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Equal(typeof(ToLowerMiddlewareWithParameter), descriptor.MiddlewareType);
        Assert.Equal(nameof(ToLowerMiddlewareWithParameter), descriptor.DisplayName);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public void UseContextDelegateRecordsNullMiddlewareType()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) => next(context));

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Null(descriptor.MiddlewareType);
        Assert.False(string.IsNullOrEmpty(descriptor.DisplayName));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public void UseComponentDelegateRecordsNullMiddlewareType()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use(next => context => next(context));

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Null(descriptor.MiddlewareType);
        Assert.False(string.IsNullOrEmpty(descriptor.DisplayName));
    }

    [Fact]
    public void UseMethodGroupRecordsMethodNameAsDisplayName()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use(PassThroughAsync);

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Null(descriptor.MiddlewareType);
        Assert.Equal(nameof(PassThroughAsync), descriptor.DisplayName);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public void MiddlewareReflectsRegistrationOrder()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) => next(context))
            .Use<ToLowerMiddleware>()
            .Use<ToLowerMiddlewareWithParameter>("prefix");

        Assert.Collection(
            handler.Middleware,
            descriptor => Assert.Null(descriptor.MiddlewareType),
            descriptor => Assert.Equal(typeof(ToLowerMiddleware), descriptor.MiddlewareType),
            descriptor => Assert.Equal(typeof(ToLowerMiddlewareWithParameter), descriptor.MiddlewareType));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task MiddlewareRemainsReadableAfterPipelineIsBuiltAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<ToLowerMiddleware>();

        _ = await handler.InvokeAsync("REQUEST", TestContext.Current.CancellationToken);

        var descriptor = Assert.Single(handler.Middleware);
        Assert.Equal(typeof(ToLowerMiddleware), descriptor.MiddlewareType);
    }

    [Fact]
    public void UseNullComponentDelegateThrowsArgumentNull()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        _ = Assert.Throws<ArgumentNullException>(() =>
            handler.Use((Func<RequestMiddleware<string, string>, RequestMiddleware<string, string>>)null!));
    }

    [Fact]
    public void UseNullContextDelegateThrowsArgumentNull()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        _ = Assert.Throws<ArgumentNullException>(() =>
            handler.Use((Func<RequestContext<string, string>, RequestMiddleware<string, string>, Task>)null!));
    }

    private static Task PassThroughAsync(RequestContext<string, string> context, RequestMiddleware<string, string> next) =>
        next(context);
}
