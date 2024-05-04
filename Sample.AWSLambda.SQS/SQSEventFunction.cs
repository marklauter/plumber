using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Plumber;
using Sample.AWSLambda.SQS.Middleware;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Sample.AWSLambda.SQS;

public sealed partial class SQSEventFunction
{
    private readonly IRequestHandler<SQSEventContext, Plumber.Void> requestHandler;

    // The default constructor is called by Lambda host service once for the lifetime of the function instance, which could be up to a couple of hours.
    // That's once per cold-start, so you want to get all your setup done here.
    // Load configuration, register services, build the handler and add middleware in the default Lambda function constructor.
    // Your function handler is then invoked for every Lambda invocation.
    public SQSEventFunction()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console(new CompactJsonFormatter())
            .CreateLogger();

        // create a request handler builder
        var builder = RequestHandlerBuilder
            .New<SQSEventContext, Plumber.Void>();

        // add services
        _ = builder.Services
            .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<EventLogger>()
           .Use<MessageValidator>()
           .Use<RecordSink>()
           .Prepare();
    }

    // This method is called for every Lambda invocation.
    // Invoke the request handler to execute the pipeline.
    // In this simplifed scenario we're passing a an event context.
    // In a real-world scenario you'd probably create context instance per record on the event and invoke the handler for each one.
    public async Task ForwardEventAsync(SQSEvent e, ILambdaContext context) =>
        _ = await requestHandler.InvokeAsync(new SQSEventContext(e, context));
}
