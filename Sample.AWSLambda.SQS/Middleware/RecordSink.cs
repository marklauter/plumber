using Dialogue;

namespace Sample.AWSLambda.SQS.Middleware;

// This user-defined middleware component is the last to be added to the pipeline. In a real-world use case, it would do the actual work of processing the SQS event.
// This simplied example is just a sink that does nothing.
internal sealed class RecordSink(
    Handler<SQSEventContext, Dialogue.Void> next)
    : IMiddleware<SQSEventContext, Dialogue.Void>
{
    public Handler<SQSEventContext, Dialogue.Void> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<SQSEventContext, Dialogue.Void> context)
    {
        // always check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        // real work would go here, but this is a sink so we just eat the records

        // the sink is the end of the user defined pipeline, but we still call next because the terminal middleware component is defined by the request handler.
        return Next(context);
    }
}
