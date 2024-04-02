using Dialogue;

namespace Sample.AWSLambda.SQS.Middleware;

internal sealed class BodyCheck(
       Handler<SQSMessageContext, Dialogue.Void> next)
    : IMiddleware<SQSMessageContext, Dialogue.Void>
{
    public Handler<SQSMessageContext, Dialogue.Void> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(RequestContext<SQSMessageContext, Dialogue.Void> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (String.IsNullOrWhiteSpace(context.Request.Message.Body))
        {
            throw new InvalidOperationException("message body was empty");
        }

        if (Content.TestBody.Equals(context.Request.Message.Body, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Jello, World is bad.");
        }

        await Next(context);
    }
}
