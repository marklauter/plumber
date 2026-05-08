using Plumber;

namespace Sample.Cli;

internal sealed class ValidationMiddleware(RequestMiddleware<string, TextReport> next)
{
    public Task InvokeAsync(RequestContext<string, TextReport> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.Request))
        {
            context.Response = new TextReport(
                Original: context.Request ?? string.Empty,
                Normalized: string.Empty,
                Tokens: [],
                WordCount: 0,
                Elapsed: TimeSpan.Zero,
                ErrorMessage: "input must be non-empty");
            return Task.CompletedTask;
        }

        return next(context);
    }
}
