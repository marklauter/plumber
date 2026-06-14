using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using System.Diagnostics;

namespace Plumber.Serilog.Extensions;

internal sealed class RequestLoggerMiddleware<TRequest, TResponse>
    where TRequest : class
{
    private readonly RequestMiddleware<TRequest, TResponse> next;
    private readonly DiagnosticContext diagnosticContext;
    private readonly ILogger logger;
    private readonly LogEventLevel level;
    private readonly Action<IDiagnosticContext, RequestContext<TRequest, TResponse>>? enrichDiagnosticContext;
    private readonly MessageTemplate messageTemplate;
    private readonly Func<RequestContext<TRequest, TResponse>, IEnumerable<LogEventProperty>> getMessageTemplateProperties;
    private readonly bool throwOnException;

    public RequestLoggerMiddleware(
        RequestMiddleware<TRequest, TResponse> next,
        DiagnosticContext diagnosticContext,
        ILogger logger,
        IOptions<RequestLoggerOptions<TRequest, TResponse>> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(diagnosticContext);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        this.next = next;
        this.diagnosticContext = diagnosticContext;
        this.logger = logger.ForContext<RequestLoggerMiddleware<TRequest, TResponse>>();
        level = value.LogLevel;
        enrichDiagnosticContext = value.EnrichDiagnosticContext;
        messageTemplate = new MessageTemplateParser().Parse(value.MessageTemplate);
        getMessageTemplateProperties = value.GetMessageTemplateProperties;
        throwOnException = value.ThrowOnException;
    }

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
        var level = ex != null ? LogEventLevel.Error : this.level;

        if (!logger.IsEnabled(level))
        {
            return;
        }

        var now = DateTimeOffset.Now;

        enrichDiagnosticContext?.Invoke(diagnosticContext, context);
        // TryComplete always yields a (possibly empty) property sequence; its bool only reports whether the
        // ambient collection was still active, which doesn't change what we log, so the result is discarded.
        _ = collector.TryComplete(out var collected, out var exception);

        var properties = collected!.Concat(getMessageTemplateProperties(context));

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
    }
}
