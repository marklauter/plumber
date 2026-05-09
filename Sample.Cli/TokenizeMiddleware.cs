using Plumber;

namespace Sample.Cli;

internal sealed class TokenizeMiddleware(RequestMiddleware<string, TextReport> next)
{
    public Task InvokeAsync(RequestContext<string, TextReport> context, ITokenizer tokenizer)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (context.TryGetValue<string>(DataKeys.Normalized, out var normalized))
        {
            context.Data[DataKeys.Tokens] = tokenizer.Tokenize(normalized);
        }

        return next(context);
    }
}
