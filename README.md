## Build Status
[![.NET Tests](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml)
[![.NET Publish](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml)
[![Nuget](https://img.shields.io/badge/Nuget-v2.3.2-blue)](https://www.nuget.org/packages/MSL.Plumber.Pipeline/)
[![Nuget](https://img.shields.io/badge/.NET-6.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
[![Nuget](https://img.shields.io/badge/.NET-7.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
[![Nuget](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/)

## 
![Plumber Logo](https://raw.githubusercontent.com/marklauter/plumber/main/images/plumber.png "Plumber Logo")

# Plumber
## Middleware pipelines for host-free projects like AWS Lambda, Console, etc.
Plumber is a request-response pipeline that supports middleware delegates and classes. It provides configuration, dependency injection, and middleware pipeline services. It's useful for AWS Lambdas, Azure Functions, queue event handlers, and similar use cases.

## References
Plumber is based on this article:
[How is the ASP.NET Core Middleware Pipeline Built - Steve Gorden, July 2020](https://www.stevejgordon.co.uk/how-is-the-asp-net-core-middleware-pipeline-built)

## Getting Started
If you're not familiar with middleware pipelines, Microsoft has a [good primer on how middleware works in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0).

## Installing
To install, use the following command in your terminal or command prompt

`dotnet add package MSL.Plumber.Pipeline`

## Usage
1. Create an `IRequestHandlerBuilder<TRequest, TResponse>` by calling one of the static `RequestHandlerBuilder.Create` methods. 
1. Use `RequestHandlerBuilder.Create(string[] args)` to create a builder with the default configuration providers for `appsettings.json` files, environment variables, command line args, and user secrets.
1. Use `RequestHandlerBuilder.Create(string[] args, Action<IConfiguration, string[]> configure)` to create a builder with your own set of configuration providers.
1. Handle additional configuration scenarios through the  `IConfigurationManager Configuration` property on the builder.
1. Register services with the `IServiceCollection Services` property.
1. Use the `Build` method to create an `IRequestHandler<TRequest, TResponse>` instance.
1. Configure the request delegate pipeline by calling the `Use` methods on the request handler.
1. Call the `Prepare` method on the request handler to compile the pipeline. If you don't call `Prepare`, the pipeline will be compiled when the first request is processed.
1. Call the `InvokeAsync` method on the request handler to forward the request to the pipeline.
1. Any class that meets the following criteria can be used as middleware
    - constructor must take `RequestMiddleware<TRequest, TResponse>` as its first argument
    - must contain an `InvokeAsync` method
    - `InvokeAsync` must return a `Task`
    - `InvokeAsync` must take `RequestContext<TRequest, TResponse>` as its first argument
1. Within your middleware delegates or class implementations, always invoke `next` to pass the request context to the next delegate in the pipeline.
1. Always call `context.CancellationToken.ThrowIfCancellationRequested()` to check for cancellation requests before processing the request or invoking `next`.
1. To terminate or "short circuit" the pipeline, don't invoke `next`.
1. Set the `Response` property on the `RequestContext<TRequest, TResponse>` argument to return a value from the pipeline execution.

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
    .Create<string, string>()
    .Build();

var response = await handler.InvokeAsync(request);

Assert.True(String.IsNullOrEmpty(response));
```

### Middleware Delegate Example
In this sample, we create a request handler with user-defined middleware that converts the request to uppercase.
```csharp
var request = "Hello, World!";

var handler = RequestHandlerBuilder.Create<string, string>()
    .Build()
    .Use(async (context, next) =>
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToUpperInvariant();
        await next(context); // call next to pass the request context to the next delegate in the pipeline
    });

var response = await handler.InvokeAsync(request);

Assert.Equal(request.ToUpperInvariant(), response);
```

### Middleware Class Example
In this sample, we create a request handler with a user-defined middleware class that converts the request to lowercase.

First, we define the middleware class, which receives the next middleware delegate in the pipeline in its constructor.
The middleware is responsible for invoking the `next` delegate. You will short-circuit the pipeline if you don't invoke the `next` delegate.
An example short-circuit scenario might be a request validation middleware that returns an error response if the request is invalid. 
The middleware is also responsible for short-circuiting when the pipeline is explicitly canceled via the `context.CancellationToken`.

Constructor-based dependency injection is supported for middleware implementations, 
with the condition that the `next` delegate must be the first argument in the constructor.
```csharp
internal sealed class ToLowerMiddleware(RequestMiddleware<string, string> next)
{
    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant();

        // call next to pass the request context to the next delegate in the pipeline
        return next(context);
    }
}
```

`InvokeAsync` dependency injection is supported for middleware implementations, 
with the condition that the `RequestContext<TRequest, TResponse>` must be the first argument.
```csharp
internal sealed class ToLowerMiddleware(RequestMiddleware<string, string> next)
{
    public Task InvokeAsync(
        RequestContext<string, string> context, // context is first argument
        IMyFavoriteService service) // second argument is injected by the pipeline's service scope
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = service.DoSomethingWonderful(context.Request.ToLowerInvariant());

        // call next to pass the request context to the next delegate in the pipeline
        return next(context); 
    }
}
```

Next, we register the middleware with the request handler with the `Use<T>` method.
```csharp
var request = "Hello, World!";

var handler = RequestHandlerBuilder.Create<string, string>()
    .Build()
    .Use<ToLowerMiddleware>();

var response = await handler.InvokeAsync(request);

Assert.Equal(request.ToLowerInvariant(), response);
```

### Builder Configuration Example
Use the `IRequestHandlerBuilder.Configuration` property to add configuration providers, like `AddInMemory` or `AddJsonFile`.
```csharp
// default configuration providers are added
// 1. optional appsettings.json
// 2. optional appsettings.{env}.json
// 3. environment variables
// 4. if dev env, then user secrets
// 5. command line args
var builder = RequestHandlerBuilder.Create<string, string>(args);

// extra configuraiton providers can be added
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string> { { "MyKey", "MyValue" } });

var handler = builder.Build();
```
Provide your own set of configuration providers with the `Create` method.
```csharp
// only user specified configuration providers are added
var builder = RequestHandlerBuilder.Create<string, string>(args, (args, configuration) => 
{
    configuration
        .AddUserSecrets("appsettings.json")
        .AddInMemoryCollection(new Dictionary<string, string> { { "MyKey", "MyValue" } });
});

var handler = builder.Build();
```

### Builder Service Registration Example
Use the `IRequestHandlerBuilder.Services` property to register services.
```csharp
var builder = RequestHandlerBuilder.Create<string, string>(args);

builder.Services
    .AddSingleton<IMyService, MyService>()
    .AddSerilog();

var handler = builder.Build();
```

### Void Response Example
Some pipelines need to return a response. One example is an AWS Lambda triggered by APIGateway. These
handlers receive an `APIGatewayHttpApiV2ProxyRequest` and return an `APIGatewayHttpApiV2ProxyResponse` to the APIGateway.

However, many use cases don't require a response. For example, an AWS Lambda triggered by an SQS message.
In these cases, the `Void` type can be used as the generic argument for `TResponse`.
```csharp
public readonly struct Void { }
```

You can use it as the response type for request handlers that don't need to return a response.
```csharp
var handler = RequestHandlerBuilder.Create<string, Void>() // Void TResponse type
    .Build();
```

