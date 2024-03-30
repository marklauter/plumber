using Microsoft.Extensions.DependencyInjection;

namespace RequestPipeline.Tests;

//https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0

public class RequestHandlerTests
{
    [Fact]
    public async Task HandleRequestWithNoUserDefinedMiddlewareAsync()
    {
        var request = "Hello, World!";

        var handler = RequestHandlerBuilder.New<string, string>()
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.True(String.IsNullOrEmpty(response));
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareAsync()
    {
        var request = "Hello, World!";

        var handler = RequestHandlerBuilder.New<string, string>()
            .Build()
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                await next(context);
            })
            .Prepare();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToUpperInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestLastMiddlewareWinsAsync()
    {
        var request = "Hello, World!";

        var handler = RequestHandlerBuilder.New<string, string>()
            .Build()
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                await next(context);
            })
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToLowerInvariant();
                await next(context);
            })
            .Prepare();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    internal sealed class ToLowerMiddleware(RequestDelegate<string, string> next)
        : IMiddleware<string, string>
    {
        public RequestDelegate<string, string> Next { get; } = next
            ?? throw new ArgumentNullException(nameof(next));

        public Task InvokeAsync(RequestContext<string, string> context)
        {
            context.Response = context.Request.ToLowerInvariant();
            return Next(context);
        }
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassAsync()
    {
        var request = "Hello, World!";

        var handler = RequestHandlerBuilder.New<string, string>()
            .Build()
            .Use<ToLowerMiddleware>()
            .Prepare();

        _ = await handler.InvokeAsync(request);
        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    internal sealed class CtorMiddleware(RequestDelegate<string, string> next)
        : IMiddleware<string, string>
    {
        public RequestDelegate<string, string> Next { get; } = next
            ?? throw new ArgumentNullException(nameof(next));

        public Task InvokeAsync(RequestContext<string, string> context)
        {
            context.Response = context.Request.ToLowerInvariant();
            return Next(context);
        }
    }

    // this is the right way to build the middleware
    [Fact]
    public async Task GetMiddlewareCtorAsync()
    {
        RequestDelegate<string, string> next = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<string, string>>(context.CancellationToken)
                : Task.FromResult(context);

        using var services = new ServiceCollection().BuildServiceProvider();

        // uses the service provider to create all arguments other than the next delegate
        var middleware = ActivatorUtilities.CreateInstance<CtorMiddleware>(services, next);
        var request = "Hello, World!";
        var context = new RequestContext<string, string>(request, services, CancellationToken.None);
        await middleware.InvokeAsync(context);

        Assert.Equal(request.ToLowerInvariant(), context.Response);
    }
}

