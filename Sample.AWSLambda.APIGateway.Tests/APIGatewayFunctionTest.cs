using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Serilog;
using Serilog.Formatting.Compact;
using Xunit;
using Xunit.Abstractions;

namespace Sample.AWSLambda.APIGateway.Tests;

public class APIGatewayFunctionTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper output = output ?? throw new ArgumentNullException(nameof(output));

    [Fact]
    public async Task TestToUpperFunction()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = "Jello, Baby.",
            IsBase64Encoded = false,
            RawPath = "/echo",
            RouteKey = "POST /echo",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = "POST",
                    Path = "/echo",
                },
                RequestId = Ulid.NewUlid().ToString(),
            },
        };

        var lambdaContext = new TestLambdaContext
        {
            AwsRequestId = Guid.NewGuid().ToString(),
        };

        using var function = new APIGatewayFunction(ConfigureLogger);
        var response = await function.ForwardRequestAsync(request, lambdaContext);

        var stdout = ((Xunit.Sdk.TestOutputHelper)output).Output;

        Assert.False(String.IsNullOrWhiteSpace(stdout));
        Assert.Contains(lambdaContext.AwsRequestId, stdout);

        Assert.Equal($"Echo({request.Body})", response.Body);
    }

    private void ConfigureLogger(LoggerConfiguration configuration) => configuration
        .WriteTo
        .TestOutput(output, new CompactJsonFormatter());

}
