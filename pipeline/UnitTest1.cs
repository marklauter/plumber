namespace Pipeline.Tests;

//https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/Builder/ApplicationBuilder.cs

public class RequestHandlerTests
{
    [Fact]
    public async Task HandleRequestWithNoUserDefinedMiddlewareAsync()
    {
        var request = "Hello, World!";

        var handler = new RequestHandlerBuilder<string, string>()
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.True(String.IsNullOrEmpty(response));
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareAsync()
    {
        var request = "Hello, World!";

        var handler = new RequestHandlerBuilder<string, string>()
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                await next(context);
            })
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToUpperInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestLastMiddlewareWinsAsync()
    {
        var request = "Hello, World!";

        var handler = new RequestHandlerBuilder<string, string>()
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
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    internal sealed class ToLowerMiddleware
        : IMiddleware<string, string>
    {
        public RequestDelegate<string, string> Next { get; set; } = null!;

        public Task InvokeAsync(RequestContext<string, string> context)
        {
            context.Response = context.Request.ToLowerInvariant();
            return this.Next(context);
        }
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassAsync()
    {
        var request = "Hello, World!";

        var handler = new RequestHandlerBuilder<string, string>()
            .Use<ToLowerMiddleware>()
            .Build();

        var response = await handler.InvokeAsync(request);

        Assert.Equal(request.ToLowerInvariant(), response);
    }
}

