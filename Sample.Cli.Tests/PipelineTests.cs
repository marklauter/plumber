namespace Sample.Cli.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task ValidInputProducesFullReportAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync("Hello, World! FOO", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Null(report.ErrorMessage);
        Assert.Equal("Hello, World! FOO", report.Original);
        Assert.Equal("hello, world! foo", report.Normalized);
        Assert.Equal(["hello,", "world!", "foo"], report.Tokens);
        Assert.Equal(3, report.WordCount);
        Assert.True(report.Elapsed > TimeSpan.Zero, "timing middleware should record positive elapsed time");
    }

    [Fact]
    public async Task EmptyInputShortCircuitsAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Equal("input must be non-empty", report.ErrorMessage);
        Assert.Empty(report.Tokens);
        Assert.Equal(0, report.WordCount);
    }

    [Fact]
    public async Task WhitespaceInputShortCircuitsAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync("   \t  ", TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.Equal("input must be non-empty", report.ErrorMessage);
    }

    [Fact]
    public async Task ShortCircuitStillRecordsElapsedAsync()
    {
        using var handler = Pipeline.Build([]);

        var report = await handler.InvokeAsync(string.Empty, TestContext.Current.CancellationToken);

        Assert.NotNull(report);
        Assert.True(report.Elapsed > TimeSpan.Zero, "timing should wrap validation short-circuit");
    }
}
