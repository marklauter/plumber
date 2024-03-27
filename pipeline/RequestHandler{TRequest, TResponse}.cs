using Microsoft.Extensions.DependencyInjection;

namespace Pipeline.Tests;

public class RequestHandler<TRequest, TResponse>(
    RequestDelegate<TRequest, TResponse> pipeline,
    ServiceProvider services,
    TimeSpan timeout)
    where TRequest : class
    where TResponse : class
{
    private readonly RequestDelegate<TRequest, TResponse> pipeline = pipeline
        ?? throw new ArgumentNullException(nameof(pipeline));
    private readonly ServiceProvider services = services
        ?? throw new ArgumentNullException(nameof(services));
    private readonly TimeSpan timeout = timeout;

    public async Task<TResponse?> InvokeAsync(TRequest request)
    {
        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.CancelAfter(this.timeout);

        var context = new RequestContext<TRequest, TResponse>(
            request,
            this.services,
            timeoutTokenSource.Token);

        await this.pipeline(context);

        return context.Response;
    }
}

