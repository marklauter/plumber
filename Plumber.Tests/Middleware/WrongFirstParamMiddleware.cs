using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests.Middleware;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "instantiated reflectively by MiddlewareFactory")]
internal sealed class WrongFirstParamMiddleware(RequestMiddleware<string, string> next)
{
    private readonly RequestMiddleware<string, string> next = next;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "intentional instance method for negative middleware shape test")]
    public Task InvokeAsync(string notAContext) => Task.CompletedTask;
}
