using Dialogue;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Sample.AWSLambda.SQS.Middleware;

internal sealed class MessageLogger(
    Handler<SQSMessageContext, Dialogue.Void> next,
    ILogger<MessageLogger> logger)
    : IMiddleware<SQSMessageContext, Dialogue.Void>
{
    private readonly ILogger<MessageLogger> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Handler<SQSMessageContext, Dialogue.Void> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(RequestContext<SQSMessageContext, Dialogue.Void> context)
    {
        using var logscope = logger
            .BeginScope(context.Request.Message.MessageId);

        var timer = Stopwatch.StartNew();
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await Next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error processing sqs message with id {MessageId}", context.Request.Message.MessageId);
        }
        finally
        {
            logger.LogInformation("sqs message processed in {ElapsedMilliseconds}ms", timer.ElapsedMilliseconds);
        }
    }
}
