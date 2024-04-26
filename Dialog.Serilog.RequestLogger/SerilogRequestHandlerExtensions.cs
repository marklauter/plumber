using Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dialog.Serilog;

public static class SerilogRequestHandlerExtensions
{
    public static IRequestHandler<TRequest, TResponse> UseSerilogRequestLogging<TRequest, TResponse>(
        this IRequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var options = handler
            .Services
            .GetService<IOptions<RequestLoggerOptions<TRequest, TResponse>>>()?.Value
            ?? new RequestLoggerOptions<TRequest, TResponse>();

        return options.MessageTemplate == null
            ? throw new ArgumentException($"{nameof(options.MessageTemplate)} cannot be null.")
            : handler.Use<RequestLoggerMiddleware<TRequest, TResponse>>(options);
    }

    public static IRequestHandler<TRequest, TResponse> UseSerilogRequestLogging<TRequest, TResponse>(
        this IRequestHandler<TRequest, TResponse> handler,
        Action<RequestLoggerOptions<TRequest, TResponse>> configureOptions)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var options = handler
            .Services
            .GetService<IOptions<RequestLoggerOptions<TRequest, TResponse>>>()?.Value
            ?? new RequestLoggerOptions<TRequest, TResponse>();

        configureOptions.Invoke(options);

        return options.MessageTemplate == null
            ? throw new ArgumentException($"{nameof(options.MessageTemplate)} cannot be null.")
            : handler.Use<RequestLoggerMiddleware<TRequest, TResponse>>(options);
    }
}
