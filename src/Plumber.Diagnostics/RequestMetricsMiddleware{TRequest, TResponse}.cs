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
        var success = true;
        try
        {
            if (addDefaultMetrics)
            {
                RequestCounter.Add(1, new KeyValuePair<string, object?>("request.type", requestType));
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context);

            if (addDefaultMetrics)
            {
                // RequestContext.Elapsed is measured from the injected TimeProvider, so timing stays testable.
                RequestDuration.Record(
                    context.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("request.type", requestType),
                    new KeyValuePair<string, object?>("success", true));
            }
        }
        catch (Exception)
        {
            success = false;
            if (addDefaultMetrics)
            {
                ErrorCounter.Add(1, new KeyValuePair<string, object?>("request.type", requestType));
                RequestDuration.Record(
                    context.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("request.type", requestType),
                    new KeyValuePair<string, object?>("success", false));
            }

            if (throwOnException)
            {
                throw;
            }
        }
        finally
        {
            recordCustomMetrics?.Invoke(context, success);
        }
    }
}
