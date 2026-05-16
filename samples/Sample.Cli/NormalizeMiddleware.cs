using Microsoft.Extensions.Logging;
using Plumber;
using System.Diagnostics.CodeAnalysis;

namespace Sample.Cli;

internal sealed class NormalizeMiddleware(
    RequestMiddleware<string, TextReport> next,
    ILogger<NormalizeMiddleware> logger)
{
    [SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "sample code prefers the readable extension-method form over a source-generated LoggerMessage partial")]
    public Task InvokeAsync(RequestContext<string, TextReport> context)
    {
        context.ThrowIfCanceled();
        var normalized = context.Request.ToLowerInvariant();
        context.Data[DataKeys.Normalized] = normalized;
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("normalized {Length} chars", normalized.Length);
        }

        return next(context);
    }
}
