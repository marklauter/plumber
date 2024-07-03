using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

internal sealed class RequestHandler<TRequest, TResponse>(
    IServiceCollection services,
    TimeSpan timeout)
    : IRequestHandler<TRequest, TResponse>, IDisposable where TRequest : class
{
    private const DynamicallyAccessedMemberTypes DynamicFlags =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods;

    private readonly List<Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>>> components = [];
    private RequestMiddleware<TRequest, TResponse>? handler;
    private bool disposed;

    private readonly ServiceProvider services = services?.BuildServiceProvider() ?? throw new ArgumentNullException(nameof(services));

    public TimeSpan Timeout => timeout;

    public Task<TResponse?> InvokeAsync(TRequest request) =>
        ThrowIfDisposed()
        .Timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? InvokeInternalAsync(request, CancellationToken.None)
            : InvokeInternalAsync(request, Timeout);

    public Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken) =>
        ThrowIfDisposed()
        .Timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? InvokeInternalAsync(request, cancellationToken)
            : InvokeInternalAsync(request, Timeout, cancellationToken);

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware)
    {
        if (handler is not null)
        {
            throw new InvalidOperationException("middleware components cannot be added after the pipeline has been built.");
        }

        ThrowIfDisposed().components.Add(middleware);
        return this;
    }

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicFlags)] TMiddleware>(params object[] parameters)
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), services, next, parameters)
                .CreateMiddleware());

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicFlags)] TMiddleware>()
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), services, next, null)
                .CreateMiddleware());

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

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutTokenSource.Token);
        return await InvokeInternalAsync(request, linkedTokenSource.Token);
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
    private static RequestMiddleware<TRequest, TResponse> Terminal() => context =>
        context.CancellationToken.IsCancellationRequested
            ? Task.FromCanceled<RequestContext<TRequest, TResponse>>(context.CancellationToken)
            : Task.FromResult(context);

    private sealed class MiddlewareFactory<TMiddleware>
        where TMiddleware : class
    {
        private const string InvokeMethodName = "InvokeAsync";
        private static readonly Type ContextType = typeof(RequestContext<TRequest, TResponse>);

        public MiddlewareFactory(
            [DynamicallyAccessedMembers(DynamicFlags)]
            Type type,
            IServiceProvider services,
            RequestMiddleware<TRequest, TResponse> next,
            object[]? parameters)
        {
            method = type.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"{InvokeMethodName} method not found on class {type.FullName}.");

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidOperationException($"{InvokeMethodName} must return {nameof(Task)}");
            }

            var allParamTypes = method
                .GetParameters()
                .Select(p => p.ParameterType);

            if (!allParamTypes.Any() || allParamTypes.FirstOrDefault() != ContextType)
            {
                throw new InvalidOperationException($"method {method.Name} must have {ContextType.Name} as its first parameter");
            }

            injectedTypes = allParamTypes
                .Where(t => t != typeof(RequestContext<TRequest, TResponse>))
                .ToArray();

            middleware = (TMiddleware)ActivatorUtilities.CreateInstance(
                services,
                type,
                parameters is null || parameters.Length == 0 ? [next] : parameters.Prepend(next).ToArray())
                ?? throw new InvalidOperationException($"can't construct type {type.FullName}");

            handler = injectedTypes.Length == 0
                ? (Func<RequestContext<TRequest, TResponse>, Task>)Delegate
                    .CreateDelegate(typeof(Func<RequestContext<TRequest, TResponse>, Task>), middleware, method)
                : null;
        }

        private readonly TMiddleware middleware;
        private readonly MethodInfo method;
        private readonly Type[] injectedTypes;
        private readonly Func<RequestContext<TRequest, TResponse>, Task>? handler;

        public RequestMiddleware<TRequest, TResponse> CreateMiddleware() =>
            handler is not null
                ? CreateDirectMiddleware()
                : CreateInjectedMiddleware();

        private RequestMiddleware<TRequest, TResponse> CreateDirectMiddleware() => context => handler!.Invoke(context);

        private RequestMiddleware<TRequest, TResponse> CreateInjectedMiddleware() =>
            context =>
            {
                var args = new object[injectedTypes.Length + 1];
                args[0] = context;
                for (var i = 1; i < args.Length; ++i)
                {
                    args[i] = context.Services.GetRequiredService(injectedTypes[i - 1]);
                }

                return (Task)method.Invoke(middleware, args)!;
            };
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        services.Dispose();

        disposed = true;
    }

    private RequestHandler<TRequest, TResponse> ThrowIfDisposed() =>
        disposed ? throw new ObjectDisposedException(GetType().FullName) : this;

}
