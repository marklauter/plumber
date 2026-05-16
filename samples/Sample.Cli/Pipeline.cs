using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plumber;
using System.Diagnostics.CodeAnalysis;

namespace Sample.Cli;

internal static class Pipeline
{
    public static RequestHandlerBuilder<string, TextReport> CreateBuilder(string[] args) =>
        RequestHandlerBuilder.Create<string, TextReport>(args)
            .ConfigureConfiguration((config, _) => config.AddInMemoryCollection([
                new($"{TokenizerOptions.SectionName}:{nameof(TokenizerOptions.Separators)}", TokenizerOptions.Defaults.Separators),
                new($"{TokenizerOptions.SectionName}:{nameof(TokenizerOptions.RemoveEmptyEntries)}", TokenizerOptions.Defaults.RemoveEmptyEntries.ToString()),
                new($"{TokenizerOptions.SectionName}:{nameof(TokenizerOptions.TrimEntries)}", TokenizerOptions.Defaults.TrimEntries.ToString()),
            ]))
            .ConfigureLogging(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddSimpleConsole(o =>
                {
                    o.SingleLine = true;
                    o.IncludeScopes = false;
                }))
            .ConfigureServices((services, configuration) =>
            {
                var options = configuration.GetSection(TokenizerOptions.SectionName).Get<TokenizerOptions>()
                    ?? TokenizerOptions.Defaults;
                _ = services
                    .AddSingleton(options)
                    .AddSingleton<ITokenizer, WhitespaceTokenizer>();
            });

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
            .Use<TokenizeMiddleware>()
            .Use<ReportMiddleware>();

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "handler ownership transfers to caller via return value")]
    public static RequestHandler<string, TextReport> Build(string[] args) =>
        Configure(CreateBuilder(args).Build());
}
