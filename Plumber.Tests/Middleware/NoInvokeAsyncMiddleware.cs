using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests.Middleware;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "instantiated reflectively by MiddlewareFactory")]
internal sealed class NoInvokeAsyncMiddleware(RequestMiddleware<string, string> next)
{
    private readonly RequestMiddleware<string, string> next = next;
}
