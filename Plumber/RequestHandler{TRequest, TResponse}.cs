using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

internal sealed class RequestHandler<TRequest, TResponse>(
    ServiceProvider services,
    TimeSpan timeout)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : class
{
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

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware)
    {
        components.Add(middleware);
        return this;
    }

    public IRequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>(params object[] parameters)
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), services, next, parameters)
                .CreateMiddleware());

    public IRequestHandler<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TMiddleware>()
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), services, next, null)
                .CreateMiddleware());

    private sealed class MiddlewareFactory<TMiddleware>
        where TMiddleware : class
    {
        private const string InvokeMethodName = "InvokeAsync";

        public MiddlewareFactory(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
            Type type,
            IServiceProvider services,
            RequestMiddleware<TRequest, TResponse> next,
            object[]? parameters)
        {
            middleware = (TMiddleware)ActivatorUtilities.CreateInstance(
                services,
                type,
                parameters is null || parameters.Length == 0 ? [next] : parameters.Prepend(next).ToArray())
                ?? throw new InvalidOperationException($"can't construct type {type.FullName}");

            method = type.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"{InvokeMethodName} method not found on class {type.FullName}.");
            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidOperationException($"{InvokeMethodName} must return {nameof(Task)}");
            }

            injectedTypes = method
                .GetParameters()
                .Select(p => p.ParameterType)
                .Where(t => t != typeof(RequestContext<TRequest, TResponse>))
                .ToArray();
        }

        private readonly TMiddleware middleware;
        private readonly MethodInfo method;
        private readonly Type[] injectedTypes;

        public RequestMiddleware<TRequest, TResponse> CreateMiddleware() =>
            injectedTypes.Length == 0
                ? CreateDirectMiddleware()
                : CreateInjectedMiddleware();

        private RequestMiddleware<TRequest, TResponse> CreateDirectMiddleware()
        {
            var foo = (Func<RequestContext<TRequest, TResponse>, Task>)Delegate
                .CreateDelegate(typeof(Func<RequestContext<TRequest, TResponse>, Task>), middleware, method);
            return context => foo.Invoke(context)!;
        }

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
}

