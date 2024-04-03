using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

namespace Sample.AWSLambda.SQS;

// This is a sample context that combines the SQS event and the Lambda context so they can be passed together to request delegates in the pipeline.
internal sealed record SQSEventContext(
    SQSEvent SQSEvent,
    ILambdaContext LambdaContext);
