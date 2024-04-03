using Dialogue;

namespace Sample.AWSLambda.APIGateway.Middleware;

// This user-defined middleware component is the last to be added to the pipeline. In a real-world use case, it would do the actual work of processing the SQS event.
// This simplied example is just a sink that does nothing.
internal sealed class RequestHandler(
    Handler<APIGatewayProxyContext, string> next)
    : IMiddleware<APIGatewayProxyContext, string>
{
    public Handler<APIGatewayProxyContext, string> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<APIGatewayProxyContext, string> context)
    {
        // always check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        // set the response on the context. this will be returned by the Lambda function request handler method
        context.Response = $"Echo({context.Request.Input})";

        return Task.CompletedTask;
    }
}

