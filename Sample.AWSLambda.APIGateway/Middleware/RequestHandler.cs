using Amazon.Lambda.APIGatewayEvents;
using Dialogue;

namespace Sample.AWSLambda.APIGateway.Middleware;

// This user-defined middleware component is the last to be added to the pipeline. In a real-world use case, it would do the actual work of processing the SQS event.
// This simplied example is just a sink that does nothing.
internal sealed class RequestHandler(
    Handler<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> next)
    : IMiddleware<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse>
{
    public Handler<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> Next { get; } = next ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> context)
    {
        // always check for cancellation
        context.CancellationToken.ThrowIfCancellationRequested();

        // set the response on the context. this will be returned by the Lambda function request handler method
        context.Response = new APIGatewayHttpApiV2ProxyResponse
        {
            Body = $"Echo({context.Request.HttpRequest.Body})",
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" }
            },
            IsBase64Encoded = false,
        };

        return Task.CompletedTask;
    }
}

