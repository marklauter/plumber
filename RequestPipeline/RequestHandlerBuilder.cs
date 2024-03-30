namespace RequestPipeline;

public static class RequestHandlerBuilder
{
    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>()
        where TRequest : class
        where TResponse : class => new RequestHandlerBuilder<TRequest, TResponse>([]);

    public static IRequestHandlerBuilder<TRequest, TResponse> New<TRequest, TResponse>(string[] args)
        where TRequest : class
        where TResponse : class => new RequestHandlerBuilder<TRequest, TResponse>(args);
}

