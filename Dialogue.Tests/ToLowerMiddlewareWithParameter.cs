namespace Dialogue.Tests;

internal sealed class ToLowerMiddlewareWithParameter(RequestMiddleware<string, string> next, string parameter)
    : IMiddleware<string, string>
{
    public RequestMiddleware<string, string> next = next
        ?? throw new ArgumentNullException(nameof(next));
    private readonly string parameter = parameter;

    public Task InvokeAsync(RequestContext<string, string> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = $"{parameter}-{context.Request.ToLowerInvariant()}";
        return next(context);
    }
}
