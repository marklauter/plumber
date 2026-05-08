namespace Sample.Cli;

public sealed record TextReport(
    string Original,
    string Normalized,
    IReadOnlyList<string> Tokens,
    int WordCount,
    TimeSpan Elapsed,
    string? ErrorMessage);
