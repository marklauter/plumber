using Amazon.Lambda.Core;
using Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Sample.AWSLambda.APIGateway.Middleware;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Sample.AWSLambda.APIGateway;

public class APIGatewayFunction
{
    private readonly IRequestHandler<APIGatewayProxyContext, string> requestHandler;

    // The default constructor is called by Lambda host service once for the lifetime of the function instance, which could be up to a couple of hours.
    // That's once per cold-start, so you want to get all your setup done here.
    // Load configuration, register services, build the handler and add middleware in the default Lambda function constructor.
    // Your function handler is then invoked for every Lambda invocation.
    public APIGatewayFunction()
    {
        Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Verbose()
           .Enrich.FromLogContext()
           .Enrich.WithExceptionDetails()
           .WriteTo.Console(new CompactJsonFormatter())
           .CreateLogger();

        // create a request handler builder
        var builder = RequestHandlerBuilder
            .New<APIGatewayProxyContext, string>();

        // add services
        _ = builder.Services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<RequestLogger>()
           .Use<RequestHandler>()
           .Prepare();
    }

    // This method is called for every Lambda invocation.
    // Invoke the request handler to execute the pipeline.
    // In this simplifed scenario we're passing a a request context.
    public Task<string?> ForwardRequestAsync(string input, ILambdaContext context) =>
        requestHandler.InvokeAsync(new APIGatewayProxyContext(input, context));

    // for unit tests only - allows unit test to override writeto TestOutputHelper
    internal APIGatewayFunction(Action<LoggerConfiguration> configureLogger)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails();
        configureLogger(loggerConfig);
        Log.Logger = loggerConfig
            .CreateLogger();

        // create a request handler builder
        var builder = RequestHandlerBuilder
            .New<APIGatewayProxyContext, string>();

        // add services
        _ = builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<RequestLogger>()
           .Use<RequestHandler>()
           .Prepare();
    }
}
