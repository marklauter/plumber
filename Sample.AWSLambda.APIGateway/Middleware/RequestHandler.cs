using Amazon.Lambda.APIGatewayEvents;
using Plumber;

namespace Sample.AWSLambda.APIGateway.Middleware;

// This user-defined middleware component is the last to be added to the pipeline. In a real-world use case, it would do the actual work of processing the SQS event.
// This simplied example is just a sink that does nothing.
internal sealed class RequestHandler(
    RequestMiddleware<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> next)
    : IMiddleware<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse>
{
    private readonly RequestMiddleware<APIGatewayHttpProxyContext, APIGatewayHttpApiV2ProxyResponse> next = next ?? throw new ArgumentNullException(nameof(next));

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

        // this invocation is options because this is the last middleware in the pipeline
        // you could also return Task.CompletedTask to short-circuit the pipeline
        return next(context);
    }
}

