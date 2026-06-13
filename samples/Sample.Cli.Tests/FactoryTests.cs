using Plumber.Testing;

namespace Sample.Cli.Tests;

// Demonstrates PlumberApplicationFactory against the real sample pipeline.
// The factory's unit tests live in Plumber.Testing.Tests.
public sealed class FactoryTests
{
    [Fact]
    public async Task FactoryInvokesPipelineEndToEndAsync()
    {
        using var factory = new PlumberApplicationFactory<string, TextReport>(
            Pipeline.CreateBuilder,
            Pipeline.Configure);

        var report = await factory.InvokeAsync("Hello, World! FOO", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Null(report.ErrorMessage);
        Assert.Equal("hello, world! foo", report.Normalized);
        Assert.Equal(3, report.WordCount);
    }
}
