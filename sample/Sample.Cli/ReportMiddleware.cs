using Plumber;

namespace Sample.Cli;

internal sealed class ReportMiddleware(RequestMiddleware<string, TextReport> next)
{
    public Task InvokeAsync(RequestContext<string, TextReport> context)
    {
        context.ThrowIfCanceled();

        _ = context.TryGetValue<string>(DataKeys.Normalized, out var normalized);
        _ = context.TryGetValue<string[]>(DataKeys.Tokens, out var tokens);

        context.Response = new TextReport(
            Original: context.Request,
            Normalized: normalized ?? string.Empty,
            Tokens: tokens ?? [],
            WordCount: tokens?.Length ?? 0,
            Elapsed: TimeSpan.Zero,
            ErrorMessage: null);

        return next(context);
    }
}
