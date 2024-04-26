namespace Dialogue.Tests;

internal sealed class ToLowerMiddleware(RequestMiddleware<string, string> next)
        : IMiddleware<string, string>
{
    public RequestMiddleware<string, string> next = next
        ?? throw new ArgumentNullException(nameof(next));

    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant();
        return next(context);
    }
}
