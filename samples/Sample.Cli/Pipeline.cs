using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plumber;
using Plumber.Diagnostics;
using Plumber.Serilog.Extensions;
using Serilog;
using Serilog.Formatting.Compact;
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
            // Register the Microsoft.Extensions.Logging stack so middleware can take ILogger<T>, but add no
            // console provider — structured console output is Serilog's job (AddSerilogRequestLogging below).
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information))
            .ConfigureServices((services, configuration) =>
            {
                var options = configuration.GetSection(TokenizerOptions.SectionName).Get<TokenizerOptions>()
                    ?? TokenizerOptions.Defaults;
                _ = services
                    .AddSerilogRequestLogging<string, TextReport>(logger => logger.WriteTo.Console(new CompactJsonFormatter()))
                    .AddPlumberDiagnostics<string, TextReport>()
                    .AddSingleton(options)
                    .AddSingleton<ITokenizer, WhitespaceTokenizer>();
            });

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent .Use() returns the same handler instance; caller disposes")]
    public static RequestHandler<string, TextReport> Configure(RequestHandler<string, TextReport> handler) =>
        handler
            .UseRequestDiagnostics()
            .UseSerilogRequestLogging()
            // Inline timer feeds the report payload's Elapsed (which the CLI prints). The OpenTelemetry span
            // duration and the Serilog event measure observability timing separately; this one shapes output.
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
