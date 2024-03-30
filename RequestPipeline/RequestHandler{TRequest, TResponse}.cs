using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace RequestPipeline;

public interface IRequestHandler<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    Task<TResponse?> InvokeAsync(TRequest request);
    IRequestHandler<TRequest, TResponse> Prepare();
    IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>, Task> middleware);
    IRequestHandler<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware);
    IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>;
}

internal sealed class RequestHandler<TRequest, TResponse>(
    ServiceProvider services,
    TimeSpan timeout)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private readonly ServiceProvider services = services
        ?? throw new ArgumentNullException(nameof(services));
    private readonly TimeSpan timeout = timeout;
    private bool prepared;
    private RequestDelegate<TRequest, TResponse>? handler;

    private readonly List<Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>>> components = [];

    public async Task<TResponse?> InvokeAsync(TRequest request)
    {
        _ = Prepare();

        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.CancelAfter(timeout);

        var context = new RequestContext<TRequest, TResponse>(
            request,
            services,
            timeoutTokenSource.Token);

        if (handler is null)
        {
            throw new InvalidOperationException("Handler not prepared.");
        }

        await handler(context);

        return context.Response;
    }

    public IRequestHandler<TRequest, TResponse> Prepare()
    {
        if (!prepared)
        {
            handler = BuildPipeline();
        }

        prepared = true;

        return this;
    }

    private RequestDelegate<TRequest, TResponse> BuildPipeline()
    {
        RequestDelegate<TRequest, TResponse> pipeline = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
                : Task.FromResult(context);

        for (var i = components.Count - 1; i >= 0; --i)
        {
            pipeline = components[i](pipeline);
        }

        return pipeline;
    }

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware)
    {
        components.Add(middleware);
        return this;
    }

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    //public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
    //    where TMiddleware : class, IMiddleware<TRequest, TResponse> => Use(
    //        next =>
    //        async context =>
    //        {
    //            using var serviceScope = context.Services.CreateScope();
    //            var middleware = ActivatorUtilities
    //                .CreateInstance<TMiddleware>(
    //                    serviceScope.ServiceProvider,
    //                    next);

    //            await middleware.InvokeAsync(context);
    //        });

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>
    {
        RequestDelegate<TRequest, TResponse> Component(RequestDelegate<TRequest, TResponse> next)
        {
            var component = CreateMiddleware(typeof(TMiddleware), next);
            return component.Invoke;
        }

        return Use(Component);
    }

    // todo: this is impossible until after the service collection is converted into a service provider, which is why aspdotnet requires the building of the application before the middleware can be created
    // so the Use methods don't belong on the builder. 
    // the whole point of trying to implement this CreateMiddleware method is to avoid having to reinstantiate the middleware on every request
    private RequestDelegate<TRequest, TResponse> CreateMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] Type type,
        RequestDelegate<TRequest, TResponse> next)
    {
        var middleware = ActivatorUtilities
            .CreateInstance(
                services,
                type,
                next);

        var method = type.GetMethod("InvokeAsync");
        return method is null
            ? throw new InvalidOperationException("InvokeAsync not found.")
            : (context => (Task)(method.Invoke(middleware, [context]) ?? throw new InvalidOperationException("InvokeAsync must return task")));
    }
}

