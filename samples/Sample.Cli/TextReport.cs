namespace Sample.Cli;

internal sealed record TextReport(
    string Original,
    string Normalized,
    IReadOnlyList<string> Tokens,
    int WordCount,
    TimeSpan Elapsed,
    string? ErrorMessage);
