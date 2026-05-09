namespace Sample.Cli;

public sealed record TokenizerOptions(
    string Separators,
    bool RemoveEmptyEntries,
    bool TrimEntries)
{
    public const string SectionName = "Tokenizer";

    public static TokenizerOptions Defaults { get; } = new(
        Separators: " ",
        RemoveEmptyEntries: true,
        TrimEntries: true);
}
