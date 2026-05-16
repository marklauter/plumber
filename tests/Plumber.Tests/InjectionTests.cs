using Plumber.Tests.Middleware;

namespace Plumber.Tests;

public sealed class InjectionTests
{
    private sealed class Middleware(RequestMiddleware<string, string> next)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "testing something")]
        public Task InvokeAsync(RequestContext<string, string> context, IInjected injected) => next(context);
    }

    [Fact]
    public void CanReadArguments()
    {
        var type = typeof(Middleware);
        var method = type.GetMethod("InvokeAsync");
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        var mwtype = typeof(RequestContext<string, string>);
        var injtype = typeof(IInjected);
        Assert.True(parameters.Length > 0);
        Assert.Equal(mwtype, parameters[0].ParameterType);
        Assert.Equal(injtype, parameters[1].ParameterType);
    }
}
