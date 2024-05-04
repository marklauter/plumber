using Microsoft.Extensions.DependencyInjection;
using Plumber;
using Plumber.Serilog;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;

var request = "Hello, World!";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

var handlerBuilder = RequestHandlerBuilder.New<string, string>();

_ = handlerBuilder.Services
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
