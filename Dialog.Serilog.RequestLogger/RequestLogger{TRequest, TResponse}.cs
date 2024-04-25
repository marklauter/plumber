using Dialogue;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using System.Diagnostics;

namespace Dialog.Serilog.RequestLogger;

internal sealed class RequestLogger<TRequest, TResponse>(
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

    private readonly ILogger? logger = options.Logger?.ForContext<RequestLogger<TRequest, TResponse>>();

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
        var logger = this.logger ?? Log.ForContext<RequestLogger<TRequest, TResponse>>();
        var level = ex != null ? LogEventLevel.Error : this.level;
        if (!logger.IsEnabled(level))
        {
            return;
        }

        enrichDiagnosticContext?.Invoke(diagnosticContext, context);
        if (!collector.TryComplete(out var properties, out var exception))
        {
            properties = ZeroProperties;
        }

        properties = properties.Concat(getMessageTemplateProperties(context));

        var (traceId, spanId) = Activity.Current is { } activity
            ? (activity.TraceId, activity.SpanId)
            : (default(ActivityTraceId), default(ActivitySpanId));

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            ex ?? exception,
            messageTemplate,
            properties,
            traceId,
            spanId);

        logger.Write(logEvent);
    }
}
