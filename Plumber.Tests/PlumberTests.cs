using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        context.Data["key"] = "value";
        Assert.True(context.TryGetValue<string>("key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void TryGetValueFalseWhenDataIsNull()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        Assert.False(context.TryGetValue<string>("key", out var value));
    }

    [Fact]
    public void TryGetValueFalseWhenKeyNotFound()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        context.Data["key1"] = "value";
        Assert.False(context.TryGetValue<string>("key2", out var value));
    }

    [Fact]
    public void TryGetValueFalseWhenValueTypeKeyNotFound()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        context.Data["other"] = 1;
        Assert.False(context.TryGetValue<int>("missing", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetValueTrueForStoredValueType()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        context.Data["count"] = 42;
        Assert.True(context.TryGetValue<int>("count", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValueFalseWhenStoredValueIsNull()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

        context.Data["key"] = null;
        Assert.False(context.TryGetValue<string>("key", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValueFalseOnTypeMismatchDoesNotThrow()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new RequestContext<string, string>("request", Ulid.NewUlid(), DateTime.UtcNow, services, CancellationToken.None);

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
                context.CancellationToken.ThrowIfCancellationRequested();
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
                context.CancellationToken.ThrowIfCancellationRequested();
                context.Response = context.Request.ToUpperInvariant();
                return next(context);
            })
            .Use((context, next) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();
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
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<string, string>>(context.CancellationToken)
                : Task.FromResult(context);

        using var services = new ServiceCollection().BuildServiceProvider();

        // uses the service provider to create all arguments other than the next delegate
        var middleware = ActivatorUtilities.CreateInstance<CtorMiddleware>(services, next);
        var request = "Hello, World!";
        var context = new RequestContext<string, string>(
            request,
            Ulid.NewUlid(),
            DateTime.UtcNow,
            services,
            CancellationToken.None);
        await middleware.InvokeAsync(context);

        Assert.Equal(request.ToLowerInvariant(), context.Response);
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

    [Fact]
    public async Task InjectedMiddlewareReturningNullTaskThrowsDescriptiveAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddSingleton<IInjected>(new Injected("injected")))
            .Build()
            .Use<NullTaskMiddleware>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));

        Assert.Contains(nameof(NullTaskMiddleware), ex.Message);
        Assert.Contains("InvokeAsync", ex.Message);
        Assert.Contains("returned null", ex.Message);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP016:Don't use disposed instance", Justification = "test verifies the post-dispose contract")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "test disposes explicitly to assert post-dispose behavior")]
    public async Task InvokeAsyncAfterDisposeThrowsObjectDisposedAsync()
    {
        var handler = RequestHandlerBuilder.Create<string, string>().Build();
        handler.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
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
        using var handler = RequestHandlerBuilder.Create<string, string>()
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
    public async Task RequestContextIdTimestampElapsedSetPerInvocationAsync()
    {
        var ids = new List<Ulid>();
        var timestamps = new List<DateTime>();
        var elapsedOnEntry = new List<TimeSpan>();
        var elapsedAfterDelay = new List<TimeSpan>();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build()
            .Use(async (context, next) =>
            {
                ids.Add(context.Id);
                timestamps.Add(context.Timestamp);
                elapsedOnEntry.Add(context.Elapsed);
                await Task.Delay(50, context.CancellationToken);
                elapsedAfterDelay.Add(context.Elapsed);
                await next(context);
            });

        var before1 = DateTime.UtcNow;
        _ = await handler.InvokeAsync("first", TestContext.Current.CancellationToken);
        var after1 = DateTime.UtcNow;

        var before2 = DateTime.UtcNow;
        _ = await handler.InvokeAsync("second", TestContext.Current.CancellationToken);
        var after2 = DateTime.UtcNow;

        Assert.NotEqual(default, ids[0]);
        Assert.NotEqual(default, ids[1]);
        Assert.NotEqual(ids[0], ids[1]);

        Assert.InRange(timestamps[0], before1, after1);
        Assert.InRange(timestamps[1], before2, after2);

        Assert.True(elapsedOnEntry[0] >= TimeSpan.Zero);
        Assert.True(elapsedAfterDelay[0] > elapsedOnEntry[0]);
        Assert.True(elapsedOnEntry[1] >= TimeSpan.Zero);
        Assert.True(elapsedAfterDelay[1] > elapsedOnEntry[1]);
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
    public async Task TimeoutWithoutCallerTokenThrowsTimeoutExceptionAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromMilliseconds(50))
            .Use(async (context, next) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
                await next(context);
            });

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => handler.InvokeAsync("request"));
        _ = Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP013:Await in using", Justification = "IDisposable analyzer is misjudging the context")]
    public async Task TimeoutWithUncancelledCallerTokenThrowsTimeoutExceptionAsync()
    {
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromMilliseconds(50))
            .Use(async (context, next) =>
            {
                await Task.Delay(System.Threading.Timeout.Infinite, context.CancellationToken);
                await next(context);
            });

        var ex = await Assert.ThrowsAsync<TimeoutException>(
            () => handler.InvokeAsync("request", TestContext.Current.CancellationToken));
        _ = Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
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
    public async Task CallerCancellationWinsRaceAgainstTimeoutAsync()
    {
        using var cts = new CancellationTokenSource();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .Build(TimeSpan.FromMilliseconds(50))
            .Use(async (context, next) =>
            {
                await cts.CancelAsync();
                // wait long enough that the 50ms timeout token has also fired,
                // so both the caller token and the timeout token are cancelled
                // when the OperationCanceledException reaches the catch filter
                await Task.Delay(200, CancellationToken.None);
                throw new OperationCanceledException(context.CancellationToken);
            });

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.InvokeAsync("request", cts.Token));
        Assert.IsNotType<TimeoutException>(ex);
    }
}
