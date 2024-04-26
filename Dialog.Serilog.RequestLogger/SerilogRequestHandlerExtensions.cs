using Dialogue;

namespace Dialog.Serilog;

public static class SerilogRequestHandlerExtensions
{
    public static IRequestHandler<TRequest, TResponse> UseSerilogRequestlogging<TRequest, TResponse>(this IRequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        // todo: WIP
        _ = handler.Use((context, next) => Task.CompletedTask);

        return handler;
    }
}
