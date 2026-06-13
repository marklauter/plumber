[![.NET Tests](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.tests.yml)
[![.NET Publish](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml/badge.svg)](https://github.com/marklauter/plumber/actions/workflows/dotnet.publish.yml)
[![NuGet](https://img.shields.io/nuget/v/MSL.Plumber.Pipeline?logo=nuget)](https://www.nuget.org/packages/MSL.Plumber.Pipeline/)
[![Nuget](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0/)

![Plumber](https://raw.githubusercontent.com/marklauter/plumber/main/images/plumber.comic.small.png "Plumber")
![MSL Armory](https://raw.githubusercontent.com/marklauter/plumber/main/images/msl.armory.small.png "MSL Armory")

# Plumber

*Another weapon from the MSL Armory*

Middleware pipelines for host-free .NET projects. The same shape ASP.NET Core gives web apps — request, response, a chain of steps with DI and configuration — for console apps, AWS Lambdas, Azure Functions, queue consumers, file processors, and anything else that lives outside a host.

The [wiki](https://github.com/marklauter/plumber/wiki) is the full documentation: concepts, a tutorial, per-type reference, and deployment recipes.

## Packages

- **`MSL.Plumber.Pipeline`** — the core builder, handler, middleware, and request context. See [Pipeline](https://github.com/marklauter/plumber/wiki/Building-A-Pipeline).
- **`MSL.Plumber.Pipeline.Testing`** — `PlumberApplicationFactory` for exercising a real pipeline in tests. See [Testing](https://github.com/marklauter/plumber/wiki/Testing).
- **`MSL.Plumber.Serilog.Extensions`** — per-request Serilog request logging. See [Serilog Extensions](https://github.com/marklauter/plumber/wiki/Serilog-Extensions).
- **`MSL.Plumber.OpenTelemetry.Extensions`** — tracing and metrics middleware. *Planned.*

## Install

```bash
dotnet add package MSL.Plumber.Pipeline
```

Plumber targets .NET 10.

## Hello, World

```csharp
using Plumber;

using var handler = RequestHandlerBuilder
    .Create<string, string>()
    .Build();

handler.Use((context, next) =>
{
    context.Response = $"Hello, {context.Request}!";
    return next(context);
});

var greeting = await handler.InvokeAsync("World");
Console.WriteLine(greeting); // Hello, World!
```

That's the whole shape: a builder, a built handler, one or more middleware, and an `InvokeAsync` call. Each invocation gets its own DI scope and cancellation token.

## Where to go next

- **New to middleware pipelines?** Start with [Concepts](https://github.com/marklauter/plumber/wiki/Concepts), then the [Tutorial](https://github.com/marklauter/plumber/wiki/Tutorial).
- **Know the shape already?** Jump into [Building a pipeline](https://github.com/marklauter/plumber/wiki/Building-A-Pipeline), [Middleware](https://github.com/marklauter/plumber/wiki/Middleware), and [Request lifecycle](https://github.com/marklauter/plumber/wiki/Request-Lifecycle).
- **Looking for a specific scenario?** Browse the recipes — AWS Lambda, Azure Functions, queue consumers, webhooks, and more — from the [wiki home](https://github.com/marklauter/plumber/wiki).
- **Migrating from v2 or v3?** See [Migration](https://github.com/marklauter/plumber/wiki/Migration).

---
[Repository](https://github.com/marklauter/plumber) · [NuGet — Pipeline](https://www.nuget.org/packages/MSL.Plumber.Pipeline/) · [NuGet — Testing](https://www.nuget.org/packages/MSL.Plumber.Pipeline.Testing/) · [MIT License](https://github.com/marklauter/plumber/blob/main/LICENSE) · [Report an issue](https://github.com/marklauter/plumber/issues)
