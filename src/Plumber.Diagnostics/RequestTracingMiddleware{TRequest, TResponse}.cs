using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Plumber.Diagnostics;

internal sealed class RequestTracingMiddleware<TRequest, TResponse>
    where TRequest : class
{
    private static readonly ActivitySource ActivitySource = new(PlumberDiagnostics.ActivitySourceName);

    private readonly RequestMiddleware<TRequest, TResponse> next;
    private readonly string operationName;
    private readonly Action<Activity, RequestContext<TRequest, TResponse>>? enrichSpan;
    private readonly bool recordException;
    private readonly bool throwOnException;
    private readonly ActivityKind spanKind;
    private readonly bool addDefaultAttributes;

    public RequestTracingMiddleware(
        RequestMiddleware<TRequest, TResponse> next,
        IOptions<RequestTracingOptions<TRequest, TResponse>> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        this.next = next;
        operationName = value.OperationName;
        enrichSpan = value.EnrichSpan;
        recordException = value.RecordException;
        throwOnException = value.ThrowOnException;
        spanKind = value.SpanKind;
        addDefaultAttributes = value.AddDefaultAttributes;
    }

    public async Task InvokeAsync(RequestContext<TRequest, TResponse> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var activity = ActivitySource.StartActivity(operationName, spanKind);

        // No listener sampled this source, so there's no span to enrich — skip straight through.
        if (activity is null)
        {
            await next(context);
            return;
        }

        if (addDefaultAttributes)
        {
            _ = activity
                .SetTag("request.id", context.Id)
                .SetTag("request.type", typeof(TRequest).Name);
        }

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context);

            if (addDefaultAttributes)
            {
                // RequestContext.Elapsed is measured from the injected TimeProvider, so timing stays testable.
                _ = activity
                    .SetTag("request.elapsed_ms", context.Elapsed.TotalMilliseconds)
                    .SetTag("response.type", context.Response?.GetType().Name ?? "null");
            }

            enrichSpan?.Invoke(activity, context);
            _ = activity.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException)
        {
            // Cancellation — caller-initiated or a Plumber timeout, indistinguishable at this layer — is an
            // expected outcome, not a defect: leave the span Unset and record no exception event. The span's
            // own Duration already captures timing. ThrowOnException still governs propagation.
            enrichSpan?.Invoke(activity, context);

            if (throwOnException)
            {
                throw;
            }
        }
        catch (Exception ex)
        {
            if (addDefaultAttributes)
            {
                _ = activity.SetTag("request.elapsed_ms", context.Elapsed.TotalMilliseconds);
            }

            if (recordException)
            {
                // Activity.AddException (net9+) records an exception event with the OpenTelemetry
                // semantic-convention tags, so the library needs no dependency on the OpenTelemetry SDK.
                _ = activity
                    .SetStatus(ActivityStatusCode.Error, ex.Message)
                    .AddException(ex);
            }

            enrichSpan?.Invoke(activity, context);

            if (throwOnException)
            {
                throw;
            }
        }
    }
}
