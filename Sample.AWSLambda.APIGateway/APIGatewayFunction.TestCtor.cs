using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using Plumber;
using Sample.AWSLambda.APIGateway.Middleware;
using Serilog;
using Serilog.Exceptions;

namespace Sample.AWSLambda.APIGateway;

public sealed partial class APIGatewayFunction
{
    // for unit tests only - allows unit test to write to TestOutputHelper
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
            .New<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse>();

        // add services
        _ = builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<RequestLogger>()
           .Use<RequestHandler>();
    }
}
