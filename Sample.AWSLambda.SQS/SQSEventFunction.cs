using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Sample.AWSLambda.SQS.Middleware;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Sample.AWSLambda.SQS;

public class SQSEventFunction
{
    private readonly IRequestHandler<SQSMessageContext, Dialogue.Void> requestHandler;

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public SQSEventFunction()
    {
        // create a request handler builder
        var builder = RequestHandlerBuilder
            .New<SQSMessageContext, Dialogue.Void>();

        // add services
        _ = builder.Services.AddLogging();

        // build the request handler, add middleware, and prepare the pipeline
        requestHandler = builder
           .Build()
           .Use<MessageLogger>()
           .Use<BodyCheck>()
           .Prepare();
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task HandleEventAsync(SQSEvent evnt, ILambdaContext context)
    {
        foreach (var message in evnt.Records)
        {
            _ = await ProcessMessageAsync(message, context);
        }
    }

    private Task<Dialogue.Void> ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context) =>
        requestHandler.InvokeAsync(new SQSMessageContext(message, context));
}
