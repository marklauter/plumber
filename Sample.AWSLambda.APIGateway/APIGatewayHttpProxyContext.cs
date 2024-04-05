using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace Sample.AWSLambda.APIGateway;

// This is a sample context that combines the api gateway proxy request and the Lambda context so they can be passed together to request delegates in the pipeline.
internal sealed record APIGatewayHttpProxyContext(
    APIGatewayHttpApiV2ProxyRequest HttpRequest,
    ILambdaContext LambdaContext);
