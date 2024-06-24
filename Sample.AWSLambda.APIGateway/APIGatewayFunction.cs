using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using Plumber;
using Sample.AWSLambda.APIGateway.Middleware;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Sample.AWSLambda.APIGateway;

public sealed partial class APIGatewayFunction
{
    private readonly IRequestHandler<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> requestHandler;

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
            .New<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse>();

        // add services
        _ = builder.Services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<RequestLogger>()
           .Use<RequestHandler>();
    }

    // This method is called for every Lambda invocation.
    // Invoke the request handler to execute the pipeline.
    // In this simplifed scenario we're passing a a request context.
    public async Task<APIGatewayHttpApiV2ProxyResponse> ForwardRequestAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
        await requestHandler.InvokeAsync(new APIGatewayHttpProxyContext(request, context))
            ?? throw new InvalidOperationException("response was unassigned");
}
