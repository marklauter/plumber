using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plumber.Tests;

public sealed class InjectionTests
{
    private interface IInjected;

    private sealed record Injected(string Message) : IInjected;

    private sealed class Middleware(RequestMiddleware<string, string> next)
    {
        public Task InvokeAsync(RequestContext<string, string> context, IInjected injected)
        {
            return next(context);
        }
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
