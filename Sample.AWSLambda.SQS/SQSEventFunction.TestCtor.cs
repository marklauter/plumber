using Microsoft.Extensions.DependencyInjection;
using Plumber;
using Sample.AWSLambda.SQS.Middleware;
using Serilog;
using Serilog.Exceptions;

namespace Sample.AWSLambda.SQS;

public sealed partial class SQSEventFunction
{
    // for unit tests only - allows unit test to write to TestOutputHelper
    internal SQSEventFunction(Action<LoggerConfiguration> configureLogger)
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
            .New<SQSEventContext, Plumber.Void>();

        // add services
        _ = builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<EventLogger>()
           .Use<MessageValidator>()
           .Use<RecordSink>()
           .Prepare();
    }
}
