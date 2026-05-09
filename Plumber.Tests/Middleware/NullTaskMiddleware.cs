namespace Plumber.Tests.Middleware;

internal sealed class NullTaskMiddleware(RequestMiddleware<string, string> next)
{
    public Task InvokeAsync(RequestContext<string, string> context, IInjected injected)
    {
        _ = next;
        _ = context;
        _ = injected;
        return null!;
    }
}
