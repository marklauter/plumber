using Microsoft.Extensions.Logging;
using Plumber;

namespace Sample.Cli;

internal sealed class NormalizeMiddleware(
    RequestMiddleware<string, TextReport> next,
    ILogger<NormalizeMiddleware> logger)
{
    public Task InvokeAsync(RequestContext<string, TextReport> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var normalized = context.Request.ToLowerInvariant();
        context.Data[DataKeys.Normalized] = normalized;
        logger.LogDebug("normalized {Length} chars", normalized.Length);
        return next(context);
    }
}
