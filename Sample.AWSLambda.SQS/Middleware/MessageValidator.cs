using Dialogue;

namespace Sample.AWSLambda.SQS.Middleware;

// This sample message validator middleware checks for empty request bodies. In a real-world scenario, you might validate the MD5 hash, check the message size, or verify the message signature.
internal sealed class MessageValidator(
    Handler<SQSEventContext, Dialogue.Void> next)
    : IMiddleware<SQSEventContext, Dialogue.Void>
{
    public Handler<SQSEventContext, Dialogue.Void> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<SQSEventContext, Dialogue.Void> context)
    {
        // always check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        var e = context.Request.SQSEvent;
        foreach (var record in e.Records)
        {
            if (String.IsNullOrWhiteSpace(record.Body))
            {
                throw new InvalidOperationException($"message id {record.MessageId} was empty");
            }
        }

        return Next(context);
    }
}
