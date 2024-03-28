namespace Pipeline.Tests;

public interface IMiddleware<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    RequestDelegate<TRequest, TResponse> Next { get; }
    Task InvokeAsync(RequestContext<TRequest, TResponse> context);
}

