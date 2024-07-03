using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Serilog;
using Serilog.Formatting.Compact;
using Xunit;
using Xunit.Abstractions;

namespace Sample.AWSLambda.SQS.Tests;

public class FunctionTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper output = output ?? throw new ArgumentNullException(nameof(output));

    [Fact]
    public async Task TestSQSEventLambdaFunctionAsync()
    {
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                new() {
                    Body = "Jello, Baby."
                }
            ]
        };

        var lambdaContext = new TestLambdaContext
        {
            AwsRequestId = Guid.NewGuid().ToString(),
        };

        using var function = new SQSEventFunction(ConfigureLogger);
        await function.ForwardEventAsync(sqsEvent, lambdaContext);

        var stdout = ((Xunit.Sdk.TestOutputHelper)output).Output;

        Assert.False(String.IsNullOrWhiteSpace(stdout));
        Assert.Contains(lambdaContext.AwsRequestId, stdout);
    }

    private void ConfigureLogger(LoggerConfiguration configuration) => configuration
        .WriteTo
        .TestOutput(output, new CompactJsonFormatter());
}
