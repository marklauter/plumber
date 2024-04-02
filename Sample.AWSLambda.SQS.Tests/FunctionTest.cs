using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Sample.AWSLambda.SQS.Tests;

public class FunctionTest
{
    [Fact]
    public async Task TestSQSEventLambdaFunctionAsync()
    {
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                new() {
                    Body = Content.TestBody
                }
            ]
        };

        var function = new SQSEventFunction();
        await function.HandleEventAsync(sqsEvent, new TestLambdaContext());

        // todo: add xunit test helper w/ serilog
        Assert.True(true);
    }
}
