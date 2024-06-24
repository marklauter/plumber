namespace Plumber.Tests.Middleware;

internal sealed class ToLowerMiddlewareWithParameter(RequestMiddleware<string, string> next, string parameter)
{
    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = $"{parameter}-{context.Request.ToLowerInvariant()}";
        return next(context);
    }
}
