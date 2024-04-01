using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Dialogue;

/// <inheritdoc/>
internal sealed class RequestHandler<TRequest, TResponse>(
    ServiceProvider services,
    TimeSpan timeout)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : class
{
    private readonly ServiceProvider services = services
        ?? throw new ArgumentNullException(nameof(services));
    private readonly TimeSpan timeout = timeout;
    private readonly List<Func<Handler<TRequest, TResponse>, Handler<TRequest, TResponse>>> components = [];

    private Handler<TRequest, TResponse>? handler;

    /// <inheritdoc/>
    public Task<TResponse?> InvokeAsync(TRequest request)
    {
        _ = Prepare();

        return handler is null
            ? throw new InvalidOperationException("Handler not prepared.")
            : timeout == Timeout.InfiniteTimeSpan
                ? InvokeInternalAsync(request, handler)
                : InvokeInternalAsync(request, handler, timeout);
    }

    private async Task<TResponse?> InvokeInternalAsync(
        TRequest request,
        Handler<TRequest, TResponse> handler)
    {
        using var serviceScope = services.CreateScope();

        var context = new RequestContext<TRequest, TResponse>(
            request,
            Ulid.NewUlid(),
            DateTime.UtcNow,
            serviceScope.ServiceProvider,
            CancellationToken.None);

        await handler(context);

        return context.Response;
    }

    private async Task<TResponse?> InvokeInternalAsync(
        TRequest request,
        Handler<TRequest, TResponse> handler,
        TimeSpan timeout)
    {
        using var serviceScope = services.CreateScope();
        using var timeoutTokenSource = new CancellationTokenSource(timeout);

        var context = new RequestContext<TRequest, TResponse>(
            request,
            Ulid.NewUlid(),
            DateTime.UtcNow,
            serviceScope.ServiceProvider,
            timeoutTokenSource.Token);

        await handler(context);

        return context.Response;
    }

    /// <inheritdoc/>
    public IRequestHandler<TRequest, TResponse> Prepare()
    {
        handler ??= BuildPipeline();
        return this;
    }

    private Handler<TRequest, TResponse> BuildPipeline()
    {
        Handler<TRequest, TResponse> pipeline = context =>
            context.CancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
                : Task.FromResult(context);

        for (var i = components.Count - 1; i >= 0; --i)
        {
            pipeline = components[i](pipeline);
        }

        return pipeline;
    }

    /// <inheritdoc/>
    public IRequestHandler<TRequest, TResponse> Use(Func<Handler<TRequest, TResponse>, Handler<TRequest, TResponse>> middleware)
    {
        components.Add(middleware);
        return this;
    }

    /// <inheritdoc/>
    public IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, Handler<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    /// <inheritdoc/>
    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class, IMiddleware<TRequest, TResponse>
    {
        Handler<TRequest, TResponse> Component(Handler<TRequest, TResponse> next)
        {
            var component = CreateMiddleware(typeof(TMiddleware), next);
            return component.Invoke;
        }

        return Use(Component);
    }

    private Handler<TRequest, TResponse> CreateMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] Type type,
        Handler<TRequest, TResponse> next)
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

