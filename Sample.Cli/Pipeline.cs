using Plumber;
using System.Diagnostics.CodeAnalysis;

namespace Sample.Cli;

internal static class Pipeline
{
    public static RequestHandlerBuilder<string, TextReport> CreateBuilder(string[] args) =>
        RequestHandlerBuilder.Create<string, TextReport>(args);

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent .Use() returns the same handler instance; caller disposes")]
    public static RequestHandler<string, TextReport> Configure(RequestHandler<string, TextReport> handler) =>
        handler
            .Use(async (context, next) =>
            {
                var start = DateTime.UtcNow;
                await next(context);
                if (context.Response is { } response)
                {
                    context.Response = response with { Elapsed = DateTime.UtcNow - start };
                }
            })
            .Use<ValidationMiddleware>()
            .Use<NormalizeMiddleware>()
            .Use((context, next) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.TryGetValue<string>(DataKeys.Normalized, out var normalized))
                {
                    context.Data[DataKeys.Tokens] = normalized.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
                return next(context);
            })
            .Use<ReportMiddleware>();

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "handler ownership transfers to caller via return value")]
    public static RequestHandler<string, TextReport> Build(string[] args) =>
        Configure(CreateBuilder(args).Build());
}
