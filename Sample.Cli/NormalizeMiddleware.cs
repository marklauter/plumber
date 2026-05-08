using Plumber;

namespace Sample.Cli;

internal sealed class NormalizeMiddleware(RequestMiddleware<string, TextReport> next)
{
    public Task InvokeAsync(RequestContext<string, TextReport> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Data[DataKeys.Normalized] = context.Request.ToLowerInvariant();
        return next(context);
    }
}
