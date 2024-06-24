using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Plumber;

internal sealed class RequestHandler<TRequest, TResponse>(
    ServiceProvider services,
    TimeSpan timeout)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : class
{
    private const string InvokeMethodName = "InvokeAsync";
    private readonly List<Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>>> components = [];
    private RequestMiddleware<TRequest, TResponse>? handler;

    public ServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
    public TimeSpan Timeout { get; } = timeout;

    public Task<TResponse?> InvokeAsync(TRequest request) =>
        Timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? InvokeInternalAsync(request, CancellationToken.None)
            : InvokeInternalAsync(request, Timeout);

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, CancellationToken cancellationToken)
    {
        using var serviceScope = services.CreateScope();
        var context = new RequestContext<TRequest, TResponse>(
            request,
            Ulid.NewUlid(),
            DateTime.UtcNow,
            serviceScope.ServiceProvider,
            cancellationToken);

        await EnsureHandler()(context);

        return context.Response;
    }

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, TimeSpan timeout)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        return await InvokeInternalAsync(request, timeoutTokenSource.Token);
    }

    private RequestMiddleware<TRequest, TResponse> EnsureHandler() => handler ??= BuildPipeline();

    private RequestMiddleware<TRequest, TResponse> BuildPipeline()
    {
        var pipeline = Terminal();
        for (var i = components.Count - 1; i >= 0; --i)
        {
            pipeline = components[i](pipeline);
        }

        return pipeline;
    }

    // the terminal middleware in the pipeline is a a no-op, or sink, that returns the context
    private static RequestMiddleware<TRequest, TResponse> Terminal() => context => context.CancellationToken.IsCancellationRequested
        ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
        : Task.FromResult(context);

    // this is a good place to start working out how to inject services into the middleware 
    public IRequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware)
    {
        components.Add(middleware);
        return this;
    }

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class =>
        Use(next => CreateMiddleware(typeof(TMiddleware), next, null).Invoke);

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>(params object[] parameters)
        where TMiddleware : class =>
        Use(next => CreateMiddleware(typeof(TMiddleware), next, parameters).Invoke);

    private RequestMiddleware<TRequest, TResponse> CreateMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] Type type,
        RequestMiddleware<TRequest, TResponse> next,
        object[]? parameters)
    {
        var middleware = ActivatorUtilities.CreateInstance(
            services,
            type,
            parameters is null ? [next] : parameters.Prepend(next).ToArray());

        var method = type.GetMethod(InvokeMethodName);
        return method is null
            ? throw new InvalidOperationException($"{InvokeMethodName} not present on class {type.FullName}.")
            : (context => (Task)(method.Invoke(middleware, [context])
                ?? throw new InvalidOperationException($"{InvokeMethodName} must return task")));
    }
}

