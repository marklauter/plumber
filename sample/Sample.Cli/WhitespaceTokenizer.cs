namespace Sample.Cli;

internal sealed class WhitespaceTokenizer(TokenizerOptions options) : ITokenizer
{
    private readonly char[] separators = options.Separators.ToCharArray();
    private readonly StringSplitOptions splitOptions =
        (options.RemoveEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None)
        | (options.TrimEntries ? StringSplitOptions.TrimEntries : StringSplitOptions.None);

    public string[] Tokenize(string input) => input.Split(separators, splitOptions);
}
