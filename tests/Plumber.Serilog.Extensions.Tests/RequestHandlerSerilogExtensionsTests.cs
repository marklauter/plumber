using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Plumber.Serilog.Extensions.Tests;

public sealed class RequestHandlerSerilogExtensionsTests
{
    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task HandleRequestSerilogRequestLoggingMiddlewareAsync()
    {
        var request = "Hello, World!";

        var sink = new TestSink();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .WriteTo.Sink(sink)
            .CreateBootstrapLogger();

        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services.AddSerilog(Log.Logger))
            .Build();

        _ = handler
            .UseSerilogRequestLogging(options =>
            {
                options.LogLevel = LogEventLevel.Information;
                options.EnrichDiagnosticContext = (diagnosticContext, context) =>
                {
                    diagnosticContext.Set(nameof(context.Request), context.Request);
                    diagnosticContext.Set(nameof(context.Response), context.Response!);
                };
            })
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);
        Assert.False(String.IsNullOrEmpty(response));
        Assert.Equal(request.ToLowerInvariant(), response);

        Assert.NotEmpty(sink.Events);
        var e = sink.Events.First();

        Assert.Equal(request, e.Properties[nameof(RequestContext<string, string>.Request)].ToString().TrimStart('"').TrimEnd('"'));
        Assert.Equal(response, e.Properties[nameof(RequestContext<string, string>.Response)].ToString().TrimStart('"').TrimEnd('"'));
        Assert.True(e.Properties.ContainsKey("RequestId"));
        Assert.True(e.Properties.ContainsKey("Elapsed"));

        // the {RequestId} token must bind to the RequestId property — a literal "{RequestId}" in the
        // rendered message means the property name and the template token drifted apart again.
        var rendered = e.RenderMessage(CultureInfo.InvariantCulture);
        Assert.DoesNotContain("{RequestId}", rendered, StringComparison.Ordinal);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task AddSerilogRequestLoggingEnablesParameterlessOverloadAsync()
    {
        var sink = new TestSink();

        // AddSerilogRequestLogging is the only registration — no AddLogging/AddOptions anywhere else.
        // It must register Serilog (logger + DiagnosticContext) AND the options infrastructure so the
        // parameterless UseSerilogRequestLogging() resolves IOptions<RequestLoggerOptions<,>> to defaults.
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSerilogRequestLogging<string, string>(logger => logger
                    .MinimumLevel.Debug()
                    .WriteTo.Sink(sink)))
            .Build();

        _ = handler
            .UseSerilogRequestLogging()
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync("Hello", TestContext.Current.CancellationToken);

        Assert.Equal("hello", response);
        Assert.NotEmpty(sink.Events);
        Assert.True(sink.Events.First().Properties.ContainsKey("RequestId"));
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task DownstreamExceptionPropagatesAndLogsAtErrorAsync()
    {
        var sink = new TestSink();
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSerilogRequestLogging<string, string>(logger => logger.MinimumLevel.Debug().WriteTo.Sink(sink)))
            .Build();

        var boom = new InvalidOperationException("boom");
        _ = handler
            .UseSerilogRequestLogging()
            .Use((_, _) => throw boom);

        // ThrowOnException defaults to true, so the original exception surfaces to the caller unchanged.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync("Hello", TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
        var e = sink.Events.First();
        Assert.Equal(LogEventLevel.Error, e.Level);
        Assert.Same(boom, e.Exception);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task ThrowOnExceptionFalseSwallowsAndLogsAtErrorAsync()
    {
        var sink = new TestSink();
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSerilogRequestLogging<string, string>(logger => logger.MinimumLevel.Debug().WriteTo.Sink(sink)))
            .Build();

        var boom = new InvalidOperationException("boom");
        _ = handler
            .UseSerilogRequestLogging(options => options.ThrowOnException = false)
            .Use((_, _) => throw boom);

        // ThrowOnException is false: the exception is swallowed, so no response is ever assigned.
        var response = await handler.InvokeAsync("Hello", TestContext.Current.CancellationToken);

        Assert.Null(response);
        var e = sink.Events.First();
        Assert.Equal(LogEventLevel.Error, e.Level);
        Assert.Same(boom, e.Exception);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task LevelBelowLoggerMinimumWritesNoEventAsync()
    {
        var sink = new TestSink();
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSerilogRequestLogging<string, string>(logger => logger.MinimumLevel.Error().WriteTo.Sink(sink)))
            .Build();

        // Options default LogLevel is Information; the logger's minimum is Error, so the completion event is suppressed.
        _ = handler
            .UseSerilogRequestLogging()
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync("Hello", TestContext.Current.CancellationToken);

        Assert.Equal("hello", response);
        Assert.Empty(sink.Events);
    }

    [Fact]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP004:Don't ignore created IDisposable",
        Justification = "fluent UseSerilogRequestLogging/Use return the same handler instance; the using var owns disposal")]
    public async Task AddSerilogRequestLoggingConfiguresOptionsThroughDiAsync()
    {
        var sink = new TestSink();
        using var handler = RequestHandlerBuilder.Create<string, string>()
            .ConfigureServices((services, _) => services
                .AddSerilogRequestLogging<string, string>(
                    logger => logger.MinimumLevel.Debug().WriteTo.Sink(sink),
                    options => options.MessageTemplate = "DONE {RequestId}"))
            .Build();

        // The parameterless overload must pick up the options configured via AddSerilogRequestLogging through IOptions.
        _ = handler
            .UseSerilogRequestLogging()
            .Use<ToLowerMiddleware>();

        _ = await handler.InvokeAsync("Hello", TestContext.Current.CancellationToken);

        var e = sink.Events.First();
        Assert.Equal("DONE {RequestId}", e.MessageTemplate.Text);
    }
}
