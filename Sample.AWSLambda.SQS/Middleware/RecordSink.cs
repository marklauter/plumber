using Dialogue;

namespace Sample.AWSLambda.SQS.Middleware;

// This user-defined middleware component is the last to be added to the pipeline. In a real-world use case, it would do the actual work of processing the SQS event.
// This simplied example is just a sink that does nothing.
internal sealed class RecordSink(
    RequestMiddleware<SQSEventContext, Dialogue.Void> next)
    : IMiddleware<SQSEventContext, Dialogue.Void>
{
    private readonly RequestMiddleware<SQSEventContext, Dialogue.Void> next = next ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<SQSEventContext, Dialogue.Void> context)
    {
        // always check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        // real work would go here, but this is a sink so we just eat the records

        // this invocation is options because this is the last middleware in the pipeline
        // you could also return Task.CompletedTask to short-circuit the pipeline
        return next(context);
    }
}
