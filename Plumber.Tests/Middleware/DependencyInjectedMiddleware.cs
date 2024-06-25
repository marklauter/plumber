namespace Plumber.Tests.Middleware;

public interface IInjected { string Value { get; } };
internal sealed record Injected(string Value) : IInjected;

internal sealed class DependencyInjectedMiddleware(RequestMiddleware<string, string> next)
{
    public Task InvokeAsync(RequestContext<string, string> context, IInjected injected)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Response = context.Request.ToLowerInvariant() + " - " + injected.Value;
        return next(context);
    }
}
