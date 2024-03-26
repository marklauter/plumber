namespace Pipeline.Tests;

//https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built
//https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-8.0
//https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http/src/Builder/ApplicationBuilder.cs

public class RequestHandlerTests
{
    [Fact]
    public async Task HandleRequestAsync()
    {
        var request = "Hello, World!";
        var context = new RequestContext<string, string>(request, CancellationToken.None);

        var handler = new RequestHandlerBuilder<string, string>()
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                await next();
            })
            .Use(async (context, next) =>
            {
                context.Response = context.Request.ToLowerInvariant();
                await next();
            })
            .Build();

        await handler(context);

        Assert.Equal(request.ToLowerInvariant(), context.Response);
    }
}

public class RequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly List<Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>>> components = [];

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware)
    {
        this.components.Add(middleware);
        return this;
    }

    public RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, Func<Task>, Task> middleware)
    {
        return this.Use(
            next =>
            context =>
            {
                Task simpleNext() => next(context);
                return middleware(context, simpleNext);
            });
    }

    public RequestDelegate<TRequest, TResponse> Build()
    {
        RequestDelegate<TRequest, TResponse> app = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
                : Task.FromResult(context);

        for (var i = this.components.Count - 1; i >= 0; i--)
        {
            app = this.components[i](app);
        }

        return app;
    }
}

public delegate Task RequestDelegate<TRequest, TResponse>(
    RequestContext<TRequest, TResponse> context)
    where TRequest : class
    where TResponse : class;

public class RequestContext<TRequest, TResponse>(
    TRequest request,
    CancellationToken cancellationToken)
    where TRequest : class
    where TResponse : class
{
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public TRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));
    public TResponse? Response { get; set; }
}