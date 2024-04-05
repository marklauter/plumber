using Amazon.Lambda.SQSEvents;
using Dialogue;
using Microsoft.Extensions.Logging;

namespace Sample.AWSLambda.SQS.Middleware;

// This sample event logger is similar to the Serilog web request logger that can be used in ASP.NET Core.
// Register the event logger ahead of the other middleware components.
internal sealed class EventLogger(
    RequestMiddleware<SQSEventContext, Dialogue.Void> next,
    ILogger<EventLogger> logger)
    : IMiddleware<SQSEventContext, Dialogue.Void>
{
    private readonly ILogger<EventLogger> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly RequestMiddleware<SQSEventContext, Dialogue.Void> next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(RequestContext<SQSEventContext, Dialogue.Void> context)
    {
        using var logscope = logger
            .BeginScope($"{nameof(SQSEvent)}::{context.Request.LambdaContext.InvokedFunctionArn}::{context.Request.LambdaContext.AwsRequestId}");

        try
        {
            // always check for cancellation
            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context); // have to await because of the using block started on line 21
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error processing SQS event");
            throw;
        }
        finally
        {
            logger.LogInformation(
                "{MessageCount} SQS messages processed in {ElapsedMilliseconds}ms with {RemainingMilliseconds}ms remaining.",
                context.Request.SQSEvent.Records.Count,
                context.Elapsed.TotalMilliseconds,
                context.Request.LambdaContext.RemainingTime.TotalMilliseconds);
        }
    }
}
