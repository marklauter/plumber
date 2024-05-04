using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace Plumber.Serilog.Tests;

public class SerilogRequestLoggerMiddlewareTests
{
    [Fact]
    public async Task HandleRequestSerilogREquestLoggingMiddlewareAsync()
    {
        var request = "Hello, World!";

        var sink = new TestSink();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .WriteTo.Sink(sink)
            .CreateBootstrapLogger();

        var handlerBuilder = RequestHandlerBuilder
            .New<string, string>();

        _ = handlerBuilder.Services
            // https://github.com/serilog/serilog-extensions-hosting/blob/dev/src/Serilog.Extensions.Hosting/SerilogHostBuilderExtensions.cs
            .AddSerilog()
            .AddLogging(loggingBuilder => loggingBuilder.AddSerilog());

        var handler = handlerBuilder.Build();

        _ = handler
            .UseSerilogRequestLogging(options =>
            {
                options.LogLevel = LogEventLevel.Information;
                options.EnrichDiagnosticContext = (diagnosticContext, context) =>
                {
                    diagnosticContext.Set(nameof(context.Request), context.Request);
                    diagnosticContext.Set(nameof(context.Response), context.Response);
                };
            })
            .Use<ToLowerMiddleware>();

        var response = await handler.InvokeAsync(request);
        Assert.False(String.IsNullOrEmpty(response));
        Assert.Equal(request.ToLowerInvariant(), response, ignoreCase: false);

        Assert.NotEmpty(sink.Events);
        var e = sink.Events.First();

        Assert.Equal(request, e.Properties[nameof(RequestContext<string, string>.Request)].ToString().TrimStart('"').TrimEnd('"'));
        Assert.Equal(response, e.Properties[nameof(RequestContext<string, string>.Response)].ToString().TrimStart('"').TrimEnd('"'));
        Assert.True(e.Properties.ContainsKey("Id"));
        Assert.True(e.Properties.ContainsKey("Elapsed"));
    }
}
