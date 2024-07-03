using Microsoft.Extensions.DependencyInjection;
using Plumber.Tests.Middleware;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests;

public class PlumberTests
{
    [Fact]
    public async Task HandleRequestWithNoUserDefinedMiddlewareAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.True(String.IsNullOrEmpty(response));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task HandleRequestWithMiddlewareAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToUpperInvariant(), response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task HandleRequestLastMiddlewareWinsAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            })
            .Use((context, next) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                context.Response = context.Request.ToLowerInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder
            .Create<string, string>()
            .Build()
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassWithCtorParameterAsync()
    {
        var request = "Hello, World!";
        var parameter = "parameter";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<ToLowerMiddlewareWithParameter>(parameter);

        var response = await handler.InvokeAsync(request);

        Assert.Equal($"{parameter}-{request.ToLowerInvariant()}", response);
    }

    /// <summary>
    /// prototype for constructing middleware with dependency injection
    /// </summary>
    [Fact]
    public async Task GetMiddlewareCtorAsync()
    {
        RequestMiddleware<string, string> next = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<string, string>>(context.CancellationToken)
                : Task.FromResult(context);

        using var services = new ServiceCollection().BuildServiceProvider();

        // uses the service provider to create all arguments other than the next delegate
        var middleware = ActivatorUtilities.CreateInstance<CtorMiddleware>(services, next);
        var request = "Hello, World!";
        var context = new RequestContext<string, string>(
            request,
            Ulid.NewUlid(),
            DateTime.UtcNow,
            services,
            CancellationToken.None);
        await middleware.InvokeAsync(context);

        Assert.Equal(request.ToLowerInvariant(), context.Response);
    }

    [Fact]
    public async Task VoidResponseType()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, Void>()
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(typeof(Void), response.GetType());
    }

    [Fact]
    public async Task InjectIntoInvokeAsync()
    {
        var request = "request";

        var builder = RequestHandlerBuilder.Create<string, string>();
        _ = builder.Services
            .AddSingleton<IInjected>(new Injected("injected"));

        using var handler = builder
            .Build()
            .Use<DependencyInjectedMiddleware>();

        var response = await handler.InvokeAsync(request);

        Assert.Equal("request - injected", response);
    }
}
