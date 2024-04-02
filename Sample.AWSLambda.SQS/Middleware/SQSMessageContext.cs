using Amazon.Lambda.Core;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace Sample.AWSLambda.SQS.Middleware;

internal sealed record SQSMessageContext(
    SQSMessage Message,
    ILambdaContext LambdaContext);
