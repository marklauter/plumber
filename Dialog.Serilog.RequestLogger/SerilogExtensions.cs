using Dialogue;
using Microsoft.Extensions.Logging;

namespace Dialog.Serilog.RequestLogger;

internal sealed class RequestLogger { }

internal sealed class RequestLogger<TRequest, TResponse>(
    RequestMiddleware<TRequest, TResponse> next,
    ILogger<RequestLogger> logger)
    : IMiddleware<TRequest, TResponse>
    where TRequest : class
{
    private readonly ILogger<RequestLogger> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly RequestMiddleware<TRequest, TResponse> next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(RequestContext<TRequest, TResponse> context)
    {
        using var logscope = logger.BeginScope(context.Id.ToString());

        logger.LogInformation("Request received at {Timestamp}", context.Timestamp);
        try
        {
            // always check for cancellation
            context.CancellationToken.ThrowIfCancellationRequested();
            await next(context); // have to await because of the using block started on line 21
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error processing SQS event");
            throw;
        }
        finally
        {
            // todo: add some log stuff
            logger.LogInformation(
                "");

        }
    }
}

public static class SerilogExtensions
{
    public static IRequestHandler<TRequest, TResponse> UseSerilogRequestlogging<TRequest, TResponse>(this IRequestHandler<TRequest, TResponse> handler)
        where TRequest : class
    {
        _ = handler.Use((context, next) => Task.CompletedTask);

        return handler;
    }
}
