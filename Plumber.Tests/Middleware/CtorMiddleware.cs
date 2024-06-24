namespace Plumber.Tests.Middleware;

internal sealed class CtorMiddleware(RequestMiddleware<string, string> next)
{
    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant();
        return next(context);
    }
}

