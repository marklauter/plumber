using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Plumber.Tests.Middleware;
using System.Diagnostics.CodeAnalysis;

namespace Plumber.Tests;

public sealed class RequestHandlerBuilderTests
{
    [Fact]
    public void BuildDisposesConfigurationWhenServiceCallbackThrows()
    {
        var probe = new DisposalProbeSource();
        var builder = RequestHandlerBuilder.Create<string, string>()
            .ConfigureConfiguration((cb, _) => cb.Sources.Add(probe))
            .ConfigureServices((_, _) => throw new InvalidOperationException("boom"));

        _ = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.True(probe.Provider.Disposed);
    }

    [Fact]
    public void BuildDisposesConfigurationWhenLoggingCallbackThrows()
    {
        var probe = new DisposalProbeSource();
        var builder = RequestHandlerBuilder.Create<string, string>()
            .ConfigureConfiguration((cb, _) => cb.Sources.Add(probe))
            .ConfigureLogging(_ => throw new InvalidOperationException("boom"));

        _ = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.True(probe.Provider.Disposed);
    }

    [Fact]
    public void BuildDisposesProviderWhenTimeProviderResolutionThrows()
    {
        TrackingDisposable? created = null;
        var builder = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSingleton<TrackingDisposable>()
                .AddSingleton<TimeProvider>(sp =>
                {
                    // force the container to create (and thereby own) a disposable, then fail
                    created = sp.GetRequiredService<TrackingDisposable>();
                    throw new InvalidOperationException("boom");
                }));

        _ = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.NotNull(created);
        Assert.True(created.Disposed, "the owned provider must be disposed when TimeProvider resolution throws in the ctor");
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task AddInMemoryCollectionExposesValuesViaConfigurationAsync()
    {
        var captured = (string?)null;

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Foo", "Bar")])
            .Build()
            .Use((context, next) =>
            {
                captured = context.Services.GetRequiredService<IConfiguration>()["Foo"];
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal("Bar", captured);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task CreateWithArgsTakesPrecedenceOverInMemoryAsync()
    {
        var captured = (string?)null;

        using var handler = RequestHandlerBuilder.Create<string, string>(["--Foo=FromArgs"])
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Foo", "FromMemory")])
            .Build()
            .Use((context, next) =>
            {
                captured = context.Services.GetRequiredService<IConfiguration>()["Foo"];
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal("FromArgs", captured);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task ChainedConfigurationOverloadsRegisterWithoutBreakingChainAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("nonexistent.json", optional: true)
            .AddJsonFile("nonexistent.reload.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddEnvironmentVariables("PLUMBER_TEST_NOPREFIX_")
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Foo", "Bar")])
            .Build()
            .Use((context, next) =>
            {
                Assert.Equal("Bar", context.Services.GetRequiredService<IConfiguration>()["Foo"]);
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);
    }

    private sealed class DisposalProbeSource : IConfigurationSource
    {
        public DisposalProbeProvider Provider { get; } = new();
        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
    }

    private sealed class DisposalProbeProvider : ConfigurationProvider, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}

public sealed class RequestContextTests
{
    [Fact]
    public void TryGetValueNotNullWhenTrue()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["key"] = "value";
        Assert.True(context.TryGetValue<string>("key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void TryGetValueFalseWhenDataIsNull()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        Assert.False(context.TryGetValue<string>("key", out var value));
    }

    [Fact]
    public void TryGetValueFalseWhenKeyNotFound()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["key1"] = "value";
        Assert.False(context.TryGetValue<string>("key2", out var value));
    }

    [Fact]
    public void TryGetValueFalseWhenValueTypeKeyNotFound()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["other"] = 1;
        Assert.False(context.TryGetValue<int>("missing", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetValueTrueForStoredValueType()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["count"] = 42;
        Assert.True(context.TryGetValue<int>("count", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValueFalseWhenStoredValueIsNull()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["key"] = null;
        Assert.False(context.TryGetValue<string>("key", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValueFalseOnTypeMismatchDoesNotThrow()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), TimeProvider.System, services, CancellationToken.None);

        context.Data["key"] = 99;
        Assert.False(context.TryGetValue<string>("key", out var value));
        Assert.Null(value);
    }
}

public sealed class PlumberTests
{
    [Fact]
    public async Task HandleRequestWithNoUserDefinedMiddlewareAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build();

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.True(string.IsNullOrEmpty(response));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task HandleRequestWithMiddlewareAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                context.ThrowIfCanceled();
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request.ToUpperInvariant(), response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task HandleRequestLastMiddlewareWinsAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                context.ThrowIfCanceled();
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            })
            .Use((context, next) =>
            {
                context.ThrowIfCanceled();
                context.Response = context.Request.ToLowerInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassAsync()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder
            .Create<string, string>()
            .Build()
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request.ToLowerInvariant(), response);
    }

    [Fact]
    public async Task HandleRequestWithMiddlewareClassWithCtorParameterAsync()
    {
        var request = "Hello, World!";
        var parameter = "parameter";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<ToLowerMiddlewareWithParameter>(parameter);

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal($"{parameter}-{request.ToLowerInvariant()}", response);
    }

    /// <summary>
    /// prototype for constructing middleware with dependency injection
    /// </summary>
    [Fact]
    public async Task GetMiddlewareCtorAsync()
    {
        RequestMiddleware<string, string> next = context =>
            context.IsCanceled
                ? Task.FromCanceled<RequestContext<string, string>>(context.CancellationToken)
                : Task.FromResult(context);

        using var services = new ServiceCollection().BuildServiceProvider();

        // uses the service provider to create all arguments other than the next delegate
        var middleware = ActivatorUtilities.CreateInstance<CtorMiddleware>(services, next);
        var request = "Hello, World!";
        var context = new RequestContext<string, string>(
            request,
            Ulid.NewUlid(),
            TimeProvider.System,
            services,
            CancellationToken.None);
        await middleware.InvokeAsync(context);

        Assert.Equal(request.ToLowerInvariant(), context.Response);
    }

    [Fact]
    public void IsCanceledReflectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        using var services = new ServiceCollection().BuildServiceProvider();

        var context = new RequestContext<string, string>(
            "request",
            Ulid.NewUlid(),
            TimeProvider.System,
            services,
            cts.Token);

        Assert.False(context.IsCanceled);

        cts.Cancel();

        Assert.True(context.IsCanceled);
    }

    [Fact]
    public void ThrowIfCanceledThrowsWhenTokenCanceled()
    {
        using var cts = new CancellationTokenSource();
        using var services = new ServiceCollection().BuildServiceProvider();

        var context = new RequestContext<string, string>(
            "request",
            Ulid.NewUlid(),
            TimeProvider.System,
            services,
            cts.Token);

        context.ThrowIfCanceled();

        cts.Cancel();

        _ = Assert.Throws<OperationCanceledException>(context.ThrowIfCanceled);
    }

    [Fact]
    public async Task UnitResponseType()
    {
        var request = "Hello, World!";

        using var handler = RequestHandlerBuilder.Create<string, Unit>()
            .Build();

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(typeof(Unit), response.GetType());
    }

    [Fact]
    public async Task InjectIntoInvokeAsync()
    {
        var request = "request";

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddSingleton<IInjected>(new Injected("injected")))
            .Build()
            .Use<DependencyInjectedMiddleware>();

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("request - injected", response);
    }

    private sealed class ScopedProbeInjectedMiddleware(RequestMiddleware<string, string> next)
    {
        public Task InvokeAsync(RequestContext<string, string> context, ScopedProbe probe)
        {
            context.Response = probe.ScopeId.ToString();
            return next(context);
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task ScopedServiceInjectedAsInvokeAsyncParamGetsFreshInstancePerInvocationAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddScoped<ScopedProbe>())
            .Build()
            .Use<ScopedProbeInjectedMiddleware>();

        var first = await handler.InvokeAsync("a", TestContext.Current.CancellationToken);
        var second = await handler.InvokeAsync("b", TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(first));
        Assert.False(string.IsNullOrEmpty(second));
        Assert.True(Guid.TryParse(first, out _));
        Assert.True(Guid.TryParse(second, out _));
        Assert.NotEqual(first, second);
    }

    private interface IDep1 { string Name { get; } }
    private interface IDep2 { string Name { get; } }
    private interface IDep3 { string Name { get; } }

    private sealed record Dep1(string Name) : IDep1;
    private sealed record Dep2(string Name) : IDep2;
    private sealed record Dep3(string Name) : IDep3;

    private sealed class ThreeDepsMiddleware(RequestMiddleware<string, string> next)
    {
        public Task InvokeAsync(RequestContext<string, string> context, IDep1 d1, IDep2 d2, IDep3 d3)
        {
            context.Response = $"{d1.Name}|{d2.Name}|{d3.Name}";
            return next(context);
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task MultipleInjectedParamsResolveInDeclarationOrderAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSingleton<IDep1>(new Dep1("one"))
                .AddSingleton<IDep2>(new Dep2("two"))
                .AddSingleton<IDep3>(new Dep3("three")))
            .Build()
            .Use<ThreeDepsMiddleware>();

        var response = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal("one|two|three", response);
    }

    private sealed class NullTaskNoInjectionMiddleware(RequestMiddleware<string, string> next)
    {
        public Task InvokeAsync(RequestContext<string, string> context)
        {
            _ = next;
            _ = context;
            return null!;
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task NoInjectionMiddlewareReturningNullTaskThrowsDescriptiveAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<NullTaskNoInjectionMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));

        Assert.Contains(nameof(NullTaskNoInjectionMiddleware), ex.Message, StringComparison.Ordinal);
        Assert.Contains("InvokeAsync", ex.Message, StringComparison.Ordinal);
        Assert.Contains("returned null", ex.Message, StringComparison.Ordinal);
    }

    private sealed class GenericTaskReturnMiddleware(RequestMiddleware<string, string> next)
    {
        public async Task<int> InvokeAsync(RequestContext<string, string> context)
        {
            context.Response = context.Request.ToUpperInvariant();
            await next(context);
            return 42;
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task ClassMiddlewareWithGenericTaskReturnTypeDispatchesCorrectlyAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<GenericTaskReturnMiddleware>();

        var response = await handler.InvokeAsync("hello", TestContext.Current.CancellationToken);

        Assert.Equal("HELLO", response);
    }

    private sealed class OnionTrace
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class OnionInjectedA(RequestMiddleware<string, string> next)
    {
        public async Task InvokeAsync(RequestContext<string, string> context, OnionTrace trace)
        {
            trace.Entries.Add("A-pre");
            await next(context);
            trace.Entries.Add("A-post");
        }
    }

    private sealed class OnionInjectedB(RequestMiddleware<string, string> next)
    {
        public async Task InvokeAsync(RequestContext<string, string> context, OnionTrace trace)
        {
            trace.Entries.Add("B-pre");
            await next(context);
            trace.Entries.Add("B-post");
        }
    }

    private sealed class OnionInjectedC(RequestMiddleware<string, string> next)
    {
        public Task InvokeAsync(RequestContext<string, string> context, OnionTrace trace)
        {
            trace.Entries.Add("core");
            return next(context);
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task InjectedClassMiddlewareExecutesInOnionOrderAsync()
    {
        OnionTrace? captured = null;

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddScoped<OnionTrace>())
            .Build()
            .Use<OnionInjectedA>()
            .Use<OnionInjectedB>()
            .Use<OnionInjectedC>()
            .Use((context, next) =>
            {
                captured = context.Services.GetRequiredService<OnionTrace>();
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(["A-pre", "B-pre", "core", "B-post", "A-post"], captured.Entries);
    }

    [Fact]
    public async Task InjectedMiddlewareReturningNullTaskThrowsDescriptiveAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddSingleton<IInjected>(new Injected("injected")))
            .Build()
            .Use<NullTaskMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));

        Assert.Contains(nameof(NullTaskMiddleware), ex.Message, StringComparison.Ordinal);
        Assert.Contains("InvokeAsync", ex.Message, StringComparison.Ordinal);
        Assert.Contains("returned null", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance", Justification = "test verifies the post-dispose contract")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly to assert post-dispose behavior")]
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "test deliberately exercises the synchronous Dispose path, which remains part of the public contract")]
    public async Task InvokeAsyncAfterDisposeThrowsObjectDisposedAsync()
    {
        var handler = RequestHandlerBuilder.Create<string, string>().Build();
        handler.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task InvokeAsyncThrowsOnNullRequestAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.InvokeAsync(null!, TestContext.Current.CancellationToken));
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "test exists specifically to exercise the no-CT InvokeAsync overload")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task InvokeAsyncNoCancellationTokenOverloadThrowsOnNullRequestAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => handler.InvokeAsync(null!));
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance", Justification = "test verifies the post-dispose contract")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly to assert post-dispose behavior")]
    public void UseAfterDisposeThrowsObjectDisposed()
    {
        var handler = RequestHandlerBuilder.Create<string, string>().Build();
        handler.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(
            () => handler.Use((context, next) => next(context)));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance", Justification = "test verifies double-dispose is safe")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly to assert idempotency")]
    public void DoubleDisposeIsSafe()
    {
        var handler = RequestHandlerBuilder.Create<string, string>().Build();
        handler.Dispose();
        handler.Dispose();
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance", Justification = "test verifies double-dispose is safe")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly to assert idempotency")]
    public async Task DoubleDisposeAsyncIsSafeAsync()
    {
        var handler = RequestHandlerBuilder.Create<string, string>().Build();
        await handler.DisposeAsync();
        await handler.DisposeAsync();
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task InvokeAsyncDisposesAsyncOnlyScopedServiceAsync()
    {
        AsyncOnlyDisposable? resolved = null;
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddScoped<AsyncOnlyDisposable>())
            .Build()
            .Use((context, next) =>
            {
                resolved = context.Services.GetRequiredService<AsyncOnlyDisposable>();
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.NotNull(resolved);
        Assert.True(resolved.Disposed, "the per-request scope must dispose async-only scoped services when the pipeline returns");
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly via DisposeAsync to assert async disposal of the owned provider")]
    public async Task DisposeAsyncDisposesAsyncOnlySingletonAsync()
    {
        AsyncOnlyDisposable? resolved = null;
        var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddSingleton<AsyncOnlyDisposable>())
            .Build();
        _ = handler.Use((context, next) =>
        {
            resolved = context.Services.GetRequiredService<AsyncOnlyDisposable>();
            return next(context);
        });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);
        await handler.DisposeAsync();

        Assert.NotNull(resolved);
        Assert.True(resolved.Disposed, "DisposeAsync must dispose async-only singletons owned by the provider");
    }

    private sealed class AsyncOnlyDisposable : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task UseAfterFirstInvokeThrowsInvalidOperationAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();
        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        _ = Assert.Throws<InvalidOperationException>(
            () => handler.Use((context, next) => next(context)));

        _ = Assert.Throws<InvalidOperationException>(
            handler.Use<ToLowerMiddleware>);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "test exists specifically to exercise the no-CT InvokeAsync overload")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task InvokeAsyncNoCancellationTokenOverloadAsync()
    {
        using var handler = RequestHandlerBuilder
            .Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync("hello");

        Assert.Equal("HELLO", response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task RequestContextIdIsUniquePerInvocationAsync()
    {
        var ids = new List<Ulid>();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use((context, next) =>
            {
                ids.Add(context.Id);
                return next(context);
            });

        _ = await handler.InvokeAsync("first", TestContext.Current.CancellationToken);
        _ = await handler.InvokeAsync("second", TestContext.Current.CancellationToken);

        Assert.NotEqual(default, ids[0]);
        Assert.NotEqual(default, ids[1]);
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task RequestContextTimestampReflectsConfiguredTimeProviderAsync()
    {
        var fakeTime = new FakeTimeProvider();
        DateTime captured = default;

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddSingleton<TimeProvider>(fakeTime))
            .Build()
            .Use((context, next) =>
            {
                captured = context.Timestamp;
                return next(context);
            });

        var expected = fakeTime.GetUtcNow().UtcDateTime;
        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal(expected, captured);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task RequestContextElapsedReflectsConfiguredTimeProviderAsync()
    {
        var fakeTime = new FakeTimeProvider();
        TimeSpan onEntry = default;
        TimeSpan afterAdvance = default;

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddSingleton<TimeProvider>(fakeTime))
            .Build()
            .Use((context, next) =>
            {
                onEntry = context.Elapsed;
                fakeTime.Advance(TimeSpan.FromSeconds(5));
                afterAdvance = context.Elapsed;
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.Zero, onEntry);
        Assert.True(afterAdvance >= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TerminalShortCircuitsWhenTokenAlreadyCancelledAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var handler = RequestHandlerBuilder.Create<string, string>().Build();

        _ = await Assert.ThrowsAsync<TaskCanceledException>(
            () => handler.InvokeAsync("request", cts.Token));
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "test exists specifically to exercise the no-CT InvokeAsync overload against a finite-timeout handler")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task FiniteTimeoutNoCancellationTokenInvokeAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromSeconds(5))
            .Use((context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync("hello");

        Assert.Equal("HELLO", response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task FiniteTimeoutInvokeCompletesBeforeTimeoutAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromSeconds(5))
            .Use((context, next) =>
            {
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            });

        var response = await handler.InvokeAsync("hello", TestContext.Current.CancellationToken);

        Assert.Equal("HELLO", response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task MiddlewareExecutesInOnionOrderAsync()
    {
        var trace = new List<string>();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use(async (context, next) =>
            {
                trace.Add("A-pre");
                await next(context);
                trace.Add("A-post");
            })
            .Use(async (context, next) =>
            {
                trace.Add("B-pre");
                await next(context);
                trace.Add("B-post");
            })
            .Use((context, next) =>
            {
                trace.Add("core");
                return next(context);
            });

        _ = await handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        Assert.Equal(["A-pre", "B-pre", "core", "B-post", "A-post"], trace);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "test exists specifically to exercise the no-CT InvokeAsync overload against a finite-timeout handler")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task FiniteTimeoutFiresAndThrowsTimeoutExceptionAsync()
    {
        var fakeTime = new FakeTimeProvider();
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddSingleton<TimeProvider>(fakeTime))
            .Build(TimeSpan.FromSeconds(30))
            .Use(async (context, next) =>
            {
                using var registration = context.CancellationToken.Register(
                    () => parked.TrySetCanceled(context.CancellationToken));
                await parked.Task;
                await next(context);
            });

        var invocation = handler.InvokeAsync("request");

        fakeTime.Advance(TimeSpan.FromSeconds(31));

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => invocation);
        _ = Assert.IsType<OperationCanceledException>(ex.InnerException, exactMatch: false);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task FiniteTimeoutFiresAndThrowsTimeoutExceptionWithCallerTokenAsync()
    {
        var fakeTime = new FakeTimeProvider();
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddSingleton<TimeProvider>(fakeTime))
            .Build(TimeSpan.FromSeconds(30))
            .Use(async (context, next) =>
            {
                using var registration = context.CancellationToken.Register(
                    () => parked.TrySetCanceled(context.CancellationToken));
                await parked.Task;
                await next(context);
            });

        var invocation = handler.InvokeAsync("request", TestContext.Current.CancellationToken);

        fakeTime.Advance(TimeSpan.FromSeconds(31));

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => invocation);
        _ = Assert.IsType<OperationCanceledException>(ex.InnerException, exactMatch: false);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task CallerCancellationStillThrowsOperationCanceledWhenTimeoutConfiguredAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromSeconds(5))
            .Use(async (context, next) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
                await next(context);
            });

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.InvokeAsync("request", cts.Token));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task UseClassMiddlewareWithoutInvokeAsyncThrowsAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<NoInvokeAsyncMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
        Assert.Contains("InvokeAsync", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task UseClassMiddlewareWithNonTaskReturnTypeThrowsAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<WrongReturnTypeMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
        Assert.Contains("Task", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task UseClassMiddlewareWithWrongFirstParamThrowsAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use<WrongFirstParamMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
        Assert.Contains("first parameter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "handler1 is intentionally disposed mid-test to verify handler2 stays functional")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP007:Don't dispose injected", Justification = "handler1 is owned by this method, intentionally disposed mid-test")]
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "test deliberately exercises the synchronous Dispose path, which remains part of the public contract")]
    public async Task BuildTwiceProducesIndependentHandlersWithPerBuildSnapshotAsync()
    {
        var builder = RequestHandlerBuilder.Create<string, string>()
            .AddInMemoryCollection([KeyValuePair.Create<string, string?>("Key1", "FromFirst")]);

        var handler1 = builder.Build()
            .Use((context, next) =>
            {
                var cfg = context.Services.GetRequiredService<IConfiguration>();
                context.Response = $"{cfg["Key1"]}|{cfg["Key2"] ?? "missing"}";
                return next(context);
            });

        _ = builder.AddInMemoryCollection([KeyValuePair.Create<string, string?>("Key2", "FromSecond")]);

        using var handler2 = builder.Build()
            .Use((context, next) =>
            {
                var cfg = context.Services.GetRequiredService<IConfiguration>();
                context.Response = $"{cfg["Key1"]}|{cfg["Key2"] ?? "missing"}";
                return next(context);
            });

        // handler1 captured sources at its Build() time — Key2 added later is invisible
        Assert.Equal("FromFirst|missing", await handler1.InvokeAsync("x", TestContext.Current.CancellationToken));
        // handler2 sees both keys
        Assert.Equal("FromFirst|FromSecond", await handler2.InvokeAsync("x", TestContext.Current.CancellationToken));

        // disposing one handler must not affect the other
        handler1.Dispose();
        Assert.Equal("FromFirst|FromSecond", await handler2.InvokeAsync("x", TestContext.Current.CancellationToken));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task CallerCancellationWinsRaceAgainstTimeoutAsync()
    {
        var fakeTime = new FakeTimeProvider();
        using var cts = new CancellationTokenSource();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddSingleton<TimeProvider>(fakeTime))
            .Build(TimeSpan.FromSeconds(5))
            .Use(async (context, next) =>
            {
                // cancel the caller token first, then advance fake time past the
                // configured timeout so both tokens are cancelled when the
                // OperationCanceledException reaches the catch filter
                await cts.CancelAsync();
                fakeTime.Advance(TimeSpan.FromSeconds(10));
                throw new OperationCanceledException(context.CancellationToken);
            });

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.InvokeAsync("request", cts.Token));
        Assert.IsNotType<TimeoutException>(ex);
    }

    private sealed class ScopedProbe
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Smoke-test fixture: counts how many times the pipeline is built across a single test.
    /// Used to confirm two <see cref="RequestHandler{TRequest, TResponse}.InvokeAsync(TRequest)"/>
    /// calls share a single built pipeline rather than rebuilding per-call. Does NOT test
    /// thread-safety of the build — that is delegated to <see cref="Lazy{T}"/> in its default
    /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> mode (BCL contract, not ours
    /// to verify). The construction counter is a <c>static</c> field, so this fixture must only
    /// be used by a single test: xUnit serializes tests within a class, but separate test
    /// classes run in parallel.
    /// </summary>
    [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline",
        Justification = "explicit static ctor exists to make counter-reset semantics visible across test cases")]
    private sealed class BuildCounterMiddleware(RequestMiddleware<string, string> next)
    {
        public static int Constructions;

        static BuildCounterMiddleware()
        {
            Constructions = 0;
        }

        private readonly RequestMiddleware<string, string> next = ConstructionTracker(next);

        private static RequestMiddleware<string, string> ConstructionTracker(RequestMiddleware<string, string> next)
        {
            _ = Interlocked.Increment(ref Constructions);
            return next;
        }

        public Task InvokeAsync(RequestContext<string, string> context) => next(context);
    }

    /// <summary>
    /// Smoke test: two <see cref="RequestHandler{TRequest, TResponse}.InvokeAsync(TRequest)"/>
    /// calls held in flight at the same time both complete and produce independent outputs.
    /// </summary>
    /// <remarks>
    /// What this test proves:
    /// <list type="bullet">
    ///   <item>The pipeline accepts a second invocation while a first is still in flight (no implicit single-flight serialization, no deadlock).</item>
    ///   <item>Each invocation receives its own <see cref="RequestContext{TRequest, TResponse}"/> and its own scoped <see cref="IServiceScope"/>.</item>
    /// </list>
    /// <para>
    /// What this test does NOT prove:
    /// <list type="bullet">
    ///   <item>Anything about <see cref="Lazy{T}"/>'s build-once-under-contention behavior. By the time the gates fire, <c>t1</c> is already past <c>handler.Value</c> with the pipeline fully built; <c>t2</c> never races the build. We delegate that invariant to the BCL.</item>
    ///   <item>That distinct <see cref="RequestContext.Id"/> or distinct DI scope require concurrency — both are also true sequentially. The asserts are kept as smoke checks; the only invariant unique to this test is "two in-flight invocations don't deadlock."</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task TwoInFlightInvocationsCompleteWithoutDeadlockAsync()
    {
        BuildCounterMiddleware.Constructions = 0;

        var firstArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new System.Collections.Concurrent.ConcurrentBag<(Ulid id, Guid scopeId)>();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((s, _) => s.AddScoped<ScopedProbe>())
            .Build()
            .Use<BuildCounterMiddleware>()
            .Use(async (context, next) =>
            {
                observed.Add((context.Id, context.Services.GetRequiredService<ScopedProbe>().ScopeId));
                if (!firstArrived.TrySetResult())
                {
                    secondArrived.SetResult();
                }

                await release.Task;
                await next(context);
            });

        var t1 = handler.InvokeAsync("a", TestContext.Current.CancellationToken);
        await firstArrived.Task;
        var t2 = handler.InvokeAsync("b", TestContext.Current.CancellationToken);
        await secondArrived.Task;

        // both invocations are now parked inside the pipeline at the same time
        release.SetResult();
        _ = await Task.WhenAll(t1, t2);

        var snapshot = observed.ToArray();
        Assert.Equal(2, snapshot.Length);
        Assert.NotEqual(snapshot[0].id, snapshot[1].id);
        Assert.NotEqual(snapshot[0].scopeId, snapshot[1].scopeId);

        // Lazy<T> built the pipeline exactly once across both overlapping invocations
        Assert.Equal(1, BuildCounterMiddleware.Constructions);
    }
}

public sealed class InjectedServiceProviderTests
{
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "handler is bound to 'using var'; analyzer is confused by the chained .Use(...) call")]
    public async Task CreateWithInjectedProviderResolvesServicesAsync()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<Greeter>()
            .BuildServiceProvider();

        using var handler = RequestHandler.Create<string, string>(provider)
            .Use((context, next) =>
            {
                context.Response = context.Services.GetRequiredService<Greeter>().Greet(context.Request);
                return next(context);
            });

        var response = await handler.InvokeAsync("world", TestContext.Current.CancellationToken);

        Assert.Equal("hello world", response);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "Test deliberately calls Dispose to assert ownership semantics on the externally-owned provider.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "tracker is intentionally not disposed; the test asserts that handler.Dispose() leaves the externally-owned tracker alive")]
    public void DisposingHandlerDoesNotDisposeInjectedProvider()
    {
        var tracker = new DisposalTracker();
        using var provider = new ServiceCollection()
            .AddSingleton(tracker)
            .BuildServiceProvider();

        using var handler = RequestHandler.Create<string, string>(provider);
        handler.Dispose();

        Assert.False(tracker.Disposed, "handler.Dispose must not dispose the externally-owned provider");

        // resolution still works against the live external provider
        Assert.Same(tracker, provider.GetRequiredService<DisposalTracker>());
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "Test deliberately calls DisposeAsync to assert ownership semantics on the externally-owned provider.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "tracker is intentionally not disposed; the test asserts that handler.DisposeAsync() leaves the externally-owned tracker alive")]
    public async Task DisposeAsyncDoesNotDisposeInjectedProviderAsync()
    {
        var tracker = new DisposalTracker();
        using var provider = new ServiceCollection()
            .AddSingleton(tracker)
            .BuildServiceProvider();

        using var handler = RequestHandler.Create<string, string>(provider);
        await handler.DisposeAsync();

        Assert.False(tracker.Disposed, "handler.DisposeAsync must not dispose the externally-owned provider");

        // resolution still works against the live external provider
        Assert.Same(tracker, provider.GetRequiredService<DisposalTracker>());
    }

    [Fact]
    public void CreateThrowsWhenInjectedProviderHasNoScopeFactory()
    {
        var stub = new NullServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () =>
            {
                using var _ = RequestHandler.Create<string, string>(stub);
            });

        Assert.Contains(nameof(IServiceScopeFactory), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "handler is bound to 'using var'; analyzer is confused by the chained .Use(...) call")]
    public async Task CreateFallsBackToSystemTimeProviderWhenNotRegisteredAsync()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        DateTime captured = default;
        using var handler = RequestHandler.Create<string, string>(provider)
            .Use((context, next) =>
            {
                captured = context.Timestamp;
                context.Response = context.Request;
                return next(context);
            });

        _ = await handler.InvokeAsync("ok", TestContext.Current.CancellationToken);

        Assert.NotEqual(default, captured);
    }

    private sealed class Greeter
    {
        private readonly string salutation = "hello";
        public string Greet(string name) => $"{salutation} {name}";
    }

    private sealed class DisposalTracker : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
