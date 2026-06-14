using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace Plumber.Diagnostics;

internal sealed class RequestMetricsMiddleware<TRequest, TResponse>
    where TRequest : class
{
    private static readonly Meter Meter = new(PlumberDiagnostics.MeterName);

    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "plumber.requests.count",
        description: "Counts the number of requests processed");

    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "plumber.requests.duration",
        unit: "ms",
        description: "Measures the duration of requests");

    private static readonly Counter<long> ErrorCounter = Meter.CreateCounter<long>(
        "plumber.requests.errors",
        description: "Counts the number of request errors");

    private readonly RequestMiddleware<TRequest, TResponse> next;
    private readonly bool addDefaultMetrics;
    private readonly Action<RequestContext<TRequest, TResponse>, bool>? recordCustomMetrics;
    private readonly bool throwOnException;

    public RequestMetricsMiddleware(
        RequestMiddleware<TRequest, TResponse> next,
        IOptions<RequestMetricsOptions<TRequest, TResponse>> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        this.next = next;
        addDefaultMetrics = value.AddDefaultMetrics;
        recordCustomMetrics = value.RecordCustomMetrics;
        throwOnException = value.ThrowOnException;
    }

    public async Task InvokeAsync(RequestContext<TRequest, TResponse> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestType = typeof(TRequest).Name;
        if (addDefaultMetrics)
        {
            // Counted up front as an attempt, so a request that later cancels or fails still counts here:
            // plumber.requests.count is throughput, and (count - errors) is not skewed by cancellation.
            RequestCounter.Add(1, new KeyValuePair<string, object?>("request.type", requestType));
        }

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Cancellation — caller-initiated or a Plumber timeout, indistinguishable at this layer — is an
            // expected outcome, not a defect: it stays out of plumber.requests.errors and the duration
            // histogram, and the custom-metrics hook (resolved outcomes only) does not fire. ThrowOnException
            // still governs propagation.
            if (throwOnException)
            {
                throw;
            }

            return;
        }
        catch (Exception)
        {
            if (addDefaultMetrics)
            {
                ErrorCounter.Add(1, new KeyValuePair<string, object?>("request.type", requestType));
                RequestDuration.Record(
                    context.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("request.type", requestType),
                    new KeyValuePair<string, object?>("success", false));
            }

            recordCustomMetrics?.Invoke(context, false);

            if (throwOnException)
            {
                throw;
            }

            return;
        }

        if (addDefaultMetrics)
        {
            // RequestContext.Elapsed is measured from the injected TimeProvider, so timing stays testable.
            RequestDuration.Record(
                context.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("request.type", requestType),
                new KeyValuePair<string, object?>("success", true));
        }

        recordCustomMetrics?.Invoke(context, true);
    }
}
