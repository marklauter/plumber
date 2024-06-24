namespace Plumber.Tests.Middleware;

internal sealed class ToLowerMiddleware(RequestMiddleware<string, string> next)
    : IMiddleware<string, string>
{
    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant();
        return next(context);
    }
}
