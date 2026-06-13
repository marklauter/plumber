using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests.Middleware;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "instantiated reflectively by MiddlewareFactory")]
internal sealed class MultipleInvokeAsyncMiddleware(RequestMiddleware<string, string> next)
{
    [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "field exists to satisfy middleware ctor shape; this negative-test middleware never calls next")]
    private readonly RequestMiddleware<string, string> next = next;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "intentional instance method for negative middleware shape test")]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "negative-test middleware; the overload exists only to model the ambiguous-InvokeAsync shape")]
    public Task InvokeAsync(RequestContext<string, string> context) => Task.CompletedTask;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "second overload exists to model the ambiguous-InvokeAsync shape under test")]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "second overload exists to model the ambiguous-InvokeAsync shape under test")]
    public Task InvokeAsync(RequestContext<string, string> context, int extra) => Task.CompletedTask;
}
