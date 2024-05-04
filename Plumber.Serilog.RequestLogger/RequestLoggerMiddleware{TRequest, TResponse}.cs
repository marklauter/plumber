using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using System.Diagnostics;

namespace Plumber.Serilog;

internal sealed class RequestLoggerMiddleware<TRequest, TResponse>(
    RequestMiddleware<TRequest, TResponse> next,
    RequestLoggerOptions<TRequest, TResponse> options,
    DiagnosticContext diagnosticContext)
    : IMiddleware<TRequest, TResponse>
    where TRequest : class
{
    private static readonly LogEventProperty[] ZeroProperties = [];

    private readonly RequestMiddleware<TRequest, TResponse> next = next
        ?? throw new ArgumentNullException(nameof(next));

    private readonly DiagnosticContext diagnosticContext = diagnosticContext
        ?? throw new ArgumentNullException(nameof(diagnosticContext));

    private readonly LogEventLevel level = options?.LogLevel
       ?? throw new ArgumentNullException(nameof(options));

    private readonly ILogger? logger = options.Logger?
        .ForContext<RequestLoggerMiddleware<TRequest, TResponse>>();

    private readonly Action<IDiagnosticContext, RequestContext<TRequest, TResponse>>? enrichDiagnosticContext =
        options.EnrichDiagnosticContext;

    private readonly MessageTemplate messageTemplate = new MessageTemplateParser()
        .Parse(options.MessageTemplate);

    private readonly Func<RequestContext<TRequest, TResponse>, IEnumerable<LogEventProperty>> getMessageTemplateProperties =
        options.GetMessageTemplateProperties;

    private readonly bool throwOnException = options.ThrowOnException;

    public async Task InvokeAsync(RequestContext<TRequest, TResponse> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var collector = diagnosticContext.BeginCollection();
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context);
            LogCompleted(context, collector, null);
        }
        catch (Exception ex)
        {
            LogCompleted(context, collector, ex);
            if (throwOnException)
            {
                throw;
            }
        }
    }

    private void LogCompleted(RequestContext<TRequest, TResponse> context, DiagnosticContextCollector collector, Exception? ex)
    {
        var logger = this.logger ?? Log.ForContext<RequestLoggerMiddleware<TRequest, TResponse>>();
        var level = ex != null ? LogEventLevel.Error : this.level;

        if (!logger.IsEnabled(level))
        {
            return;
        }

        var now = DateTimeOffset.Now;

        enrichDiagnosticContext?.Invoke(diagnosticContext, context);
        if (!collector.TryComplete(out var properties, out var exception))
        {
            properties = ZeroProperties;
        }

        properties = properties.Concat(getMessageTemplateProperties(context));

        var current = Activity.Current;
        var logEvent = new LogEvent(
            now,
            level,
            ex ?? exception,
            messageTemplate,
            properties,
            current?.TraceId ?? default,
            current?.SpanId ?? default);

        logger.Write(logEvent);
        //logger.Information("hello");
    }
}
