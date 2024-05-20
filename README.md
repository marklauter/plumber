## Build Status
[![.NET Tests](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml)
[![.NET Publish](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml)

##
![plumber logo](https://raw.githubusercontent.com/marklauter/plumber/main/images/plumber.png)

# Plumber: Pipelines for AWS Lambda
Plumber is a request pipeline that supports middleware delegates and classes. It provides configuration, dependency injection, and middleware pipeline services for AWS Lambda functions and Azure functions.

## References
Plumber is based this article:
[How is the ASP.NET Core Middleware Pipeline Built - Steve Gorden, July 2020](https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built)

## Getting Started
If you're not familiar with middleware pipelines, Microsoft has a [good primer on how middleware works in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0).

## Installing
To install, use the following command: `dotnet add package MSL.Plumber.Pipeline`

## Usage
1. Create an `IRequestHandlerBuilder<TRequest, TResponse>` by calling one of the static `RequestHandlerBuilder.New` methods. 
1. The request handler builder adds default configuration providers for appsettings files, environment variables, command line args, and user secrets.
1. Handle additional configuration scenarios through the  `IConfigurationManager Configuration` property on the builder.
1. Register services with the `IServiceCollection Servics` property.
1. Use the `Build` method to create an `IRequestRequestDelegate<TRequest, TResponse>` instance.
1. Configure the request delegate pipeline by calling the `Use` methods on the request handler.
1. Call the `Prepare` method on the request handler to compile the pipeline. If you don't call `Prepare`, the pipeline will be compiled when the first request is processed.
1. Call the `InvokeAsync` method on the request handler to forward the request to the pipeline.
1. Within your middleware delegates, or `IMiddleware` implementations, always invoke `Next` to pass the request context to the next delegate in the pipeline.
1. Always call `context.CancelationToken.ThrowIfCancellationRequested()` to check for cancellation requests before processing the request or invoking `Next`.
1. To terminate, or "short circuit", the pipeline don't invoke `Next`.
1. In your terminal delegate, set the response value in the request context.

## Sample AWS Lambda Projects
- [Samples.Lambda.SQS](https://github.com/marklauter/Plumber/tree/main/Sample.AWSLambda.SQS)
- [Samples.Lambda.SQS.Tests](https://github.com/marklauter/Plumber/tree/main/Sample.AWSLambda.SQS.Tests)
- [Samples.Lambda.APIGateway](https://github.com/marklauter/Plumber/tree/main/Sample.AWSLambda.APIGateway)
- [Samples.Lambda.APIGateway.Tests](https://github.com/marklauter/Plumber/tree/main/Sample.AWSLambda.APIGateway.Tests)

## Examples
The following examples demonstrate common usage scenarios.

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
        context.CancelationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToUpperInvariant();
        await next(context); // call next to pass the request context to the next delegate in the pipeline
    })
    .Prepare();

var response = await handler.InvokeAsync(request);

Assert.Equal(request.ToUpperInvariant(), response);
```

### Middleware Class Example
In this sample, we create a request handler with a user-defined `IMiddleware` implementation that converts the request to lowercase.

First, we define the middleware class, which receives the next middleware delegate in the pipeline in its constructor.
The middleware is responsible for calling the next delegate unless the middleware needs to short-circuiting the pipeline.
An example short-circuit scenario might be a request validation middleware that returns an error response if the request is invalid.

Constructor-based dependency injection is supported for `IMiddleware` implementations, 
with the condition that the `next` delegate must be the first argument in the constructor.
```csharp
internal sealed class ToLowerMiddleware(RequestMiddleware<string, string> next)
    : IMiddleware<string, string>
{
    private readonly RequestMiddleware<string, string> next = next
        ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(Context<string, string> context)
    {
        context.CancelationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant();
        return next(context); // call next to pass the request context to the next delegate in the pipeline
    }
}
```

Next, we register the middleware with the request handler with the `Use<T>` method.
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
Use the `IRequestHandlerBuilder.Configuration` property to add configuration providers, like `AddInMemory` or `AddJsonFile`.
```csharp
var builder = RequestHandlerBuilder.New<string, string>(args);

builder.Configuration
    .AddInMemoryCollection(new Dictionary<string, string> { { "MyKey", "MyValue" } });

var handler = builder.Build();
```

### Builder ConfigureServices Example
Use the `IRequestHandlerBuilder.Services` property to register services.
```csharp
var builder = RequestHandlerBuilder.New<string, string>(args);

builder.Services
    .AddSingleton<IMyService, MyService>()
    .AddSerilog();

var handler = builder.Build();
```

### Void Response Example
Use Void as a response type for request handlers that don't return a value.
```csharp
public readonly struct Void { }
```
For request handlers that don't return a response, use `Void` as the response type.
```csharp
var handler = RequestHandlerBuilder.New<string, Void>() // Void TResponse type
    .Build()
    .Prepare();
```
