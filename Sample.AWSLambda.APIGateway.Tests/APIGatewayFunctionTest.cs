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
        var request = "Jello, Baby.";

        var lambdaContext = new TestLambdaContext
        {
            AwsRequestId = Guid.NewGuid().ToString(),
        };

        var function = new APIGatewayFunction(ConfigureLogger);
        var response = await function.ForwardRequestAsync(request, lambdaContext);

        var stdout = ((Xunit.Sdk.TestOutputHelper)output).Output;

        Assert.False(String.IsNullOrWhiteSpace(stdout));
        Assert.Contains(lambdaContext.AwsRequestId, stdout);

        Assert.Equal($"Echo({request})", response);
    }

    private void ConfigureLogger(LoggerConfiguration configuration) => configuration
        .WriteTo
        .TestOutput(output, new CompactJsonFormatter());

}
