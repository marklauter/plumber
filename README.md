# Dialogue
Dialogue is a request pipeline that supports middleware delegates and classes. It provides configuration, dependency injection, and middleware pipeline services for AWS Lambda functions or Azure functions.

## References
[How is the ASP.NET Core Middleware Pipeline Built - Steve Gorden, July 2020](https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built)

## Getting Started
If you're not familiar with middleware pipelines, Microsoft has a [good primer on how middleware works in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0).

## Installing
To install, use the following command: `dotnet add package Dialogue`

## Usage
1. Create an `IRequestHandlerBuilder<TRequest, TResponse>` by calling one of the static `RequestHandlerBuilder.New` methods. 
2. Load configuration by calling the `Configure` method on the builder.
3. Register services with `IServiceCollection` by calling `ConfigureServices` on the builder.
4. Use the `Build` method to create an `IRequestHandler<TRequest, TResponse>` instance.
5. Configure the request delegate pipeline by calling one of the `Use` methods on the request handler.
6. Call the `Prepare` method on the request handler to compile the pipeline. If you don't call `Prepare`, the pipeline will be compiled when the first request is processed.
7. Call the `InvokeAsync` method on the request handler to process a request.
8. Within your middleware delegates, or `IMiddleware` implementations, call the `Next` method to pass the request context to the next middleware in the pipeline.

## Samples

### Simplest Example - no config, no services, no middleware
In this sample, we create a request handler that does nothing with no configuration, no service registration, and no user-defined middleware. This is the simplest possible example.
```csharp
var request = "Hello, World!";

var handler = RequestHandlerBuilder
    .New<string, string>()
    .Build();

var response = await handler.InvokeAsync(request);

Assert.True(String.IsNullOrEmpty(response));
```

### Middleware Delegate Example
In this sample, we create a request handler with user-defined middleware that converts the request to uppercase.
```csharp
var request = "Hello, World!";

var handler = RequestHandlerBuilder.New<string, string>()
    .Build()
    .Use(async (context, next) =>
    {
        context.Response = context.Request.ToUpperInvariant();
        await next(context); // call next to pass the request context to next delegate in the pipeline
    })
    .Prepare();

var response = await handler.InvokeAsync(request);

Assert.Equal(request.ToUpperInvariant(), response);
```


### Middleware Class Example
In this sample, we create a request handler with a user-defined `IMiddleware` implementation that converts the request to lowercase.

First, we define the middleware class.
```csharp
internal sealed class ToLowerMiddleware(Handler<string, string> next)
    : IMiddleware<string, string>
{
    public Handler<string, string> Next { get; } = next
        ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(Context<string, string> context)
    {
        context.Response = context.Request.ToLowerInvariant();
        return next(context); // call next to pass the request context to next delegate in the pipeline
    }
}
```

Then we register it with the request handler.
```csharp
var request = "Hello, World!";

var handler = RequestHandlerBuilder.New<string, string>()
    .Build()
    .Use<ToLowerMiddleware>()
    .Prepare();

var response = await handler.InvokeAsync(request);

Assert.Equal(request.ToLowerInvariant(), response);
```

### Builder Configure Example
Call the `Configure` method on the builder to load configuration from `appsettings.json`, environment variables, user secrets, etc.
Call `Configure` before calling `ConfigureServices`.
```csharp
var handler = RequestHandlerBuilder.New<string, string>()
    .Configure(config =>
    {
        config
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables();
    })
    .Build();
```

### Builder ConfigureServices Example
Call the `ConfigureServices` method on the builder to register services with `IServiceCollection`.
Call `Configure` before calling `ConfigureServices`.
```csharp
var handler = RequestHandlerBuilder.New<string, string>()
    .Configure(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((services, config) =>
    {
        services
            .AddLogging()
            .AddSingleton<IMyService, MyService>();
    })
    .Build();
```
