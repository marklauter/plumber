namespace Pipeline.Tests;

public delegate Task RequestDelegate<TRequest, TResponse>(
    RequestContext<TRequest, TResponse> context)
    where TRequest : class
    where TResponse : class;