using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Plumber;

/// <summary>
/// Use RequestHandler to setup and invoke the request/response pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public sealed class RequestHandler<TRequest, TResponse>
    : IDisposable
    where TRequest : notnull
{
    private readonly List<Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>>> components = [];
    private readonly ServiceProvider serviceProvider;
    private readonly TimeProvider timeProvider;
    private readonly Lazy<RequestMiddleware<TRequest, TResponse>> handler;
    private bool disposed;

    internal RequestHandler(
        ServiceCollection serviceCollection,
        TimeSpan timeout)
    {
        serviceProvider = serviceCollection.BuildServiceProvider();
        timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        handler = new Lazy<RequestMiddleware<TRequest, TResponse>>(BuildPipeline);
        Timeout = timeout;
    }

    /// <summary>
    /// The timeout for the request handler's pipeline.
    /// </summary>
    /// <remarks>
    /// When Timeout is set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> <see cref="CancellationToken.None"/> is passed to the <see cref="RequestContext{TRequest, TResponse}"/> constructor.
    /// Otherwise, a timeout-based <see cref="CancellationTokenSource"/> is used to provide the cancelation token for the request context.
    /// When the timeout elapses before the pipeline completes, <see cref="InvokeAsync(TRequest)"/> throws <see cref="TimeoutException"/> rather than <see cref="OperationCanceledException"/>, so timeouts can be distinguished from caller-initiated cancellation.
    /// </remarks>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Task{TResponse}</returns>
    /// <remarks>
    /// InvokeAsync creates a new <see cref="RequestContext{TRequest, TResponse}"/> and passes it through the request handler's pipeline.
    /// The service provider passed to the RequestContext constructor is scoped to the request handler's ServiceProvider, and is disposed of after the request handler's pipeline completes.
    /// So there is no need for users of the RequestContext.Services property to call CreateScope().
    /// </remarks>
    /// <exception cref="TimeoutException">Thrown when <see cref="Timeout"/> elapses before the pipeline completes.</exception>
    public Task<TResponse?> InvokeAsync(TRequest request) =>
        ThrowIfDisposed()
        .Timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? InvokeInternalAsync(request, CancellationToken.None)
            : InvokeInternalAsync(request, Timeout);

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task{TResponse}</returns>
    /// <remarks>
    /// InvokeAsync creates a new <see cref="RequestContext{TRequest, TResponse}"/> and passes it through the request handler's pipeline.
    /// The service provider passed to the RequestContext constructor is scoped to the request handler's ServiceProvider, and is disposed of after the request handler's pipeline completes.
    /// So there is no need for users of the RequestContext.Services property to call CreateScope().
    /// When both <paramref name="cancellationToken"/> and the internal timeout fire, caller cancellation wins (an <see cref="OperationCanceledException"/> propagates rather than a <see cref="TimeoutException"/>).
    /// </remarks>
    /// <exception cref="TimeoutException">Thrown when <see cref="Timeout"/> elapses before the pipeline completes and <paramref name="cancellationToken"/> was not cancelled.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken) =>
        ThrowIfDisposed()
        .Timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? InvokeInternalAsync(request, cancellationToken)
            : InvokeInternalAsync(request, Timeout, cancellationToken);

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T, TResult}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/></param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    public RequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware)
    {
        if (handler.IsValueCreated)
        {
            throw new InvalidOperationException("middleware components cannot be added after the pipeline has been built.");
        }

        ThrowIfDisposed().components.Add(middleware);
        return this;
    }

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware"><see cref="Func{T1, T2, TResult}"/>, <see cref="RequestContext{TRequest, TResponse}"/>, <see cref="RequestMiddleware{TRequest, TResponse}"/>, <see cref="Task"/></param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    public RequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    /// <summary>
    /// Adds a class-based middleware to the request handler's pipeline with constructor parameters.
    /// </summary>
    /// <typeparam name="TMiddleware">A class with an InvokeAsync method whose first parameter is <see cref="RequestContext{TRequest, TResponse}"/>.</typeparam>
    /// <param name="parameters">Constructor arguments for the middleware implementation.</param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    /// <remarks>
    /// <para>
    /// Constructor arguments are always passed after the Next middleware argument and before arguments provided by the service provider.
    /// </para>
    /// <para>
    /// The middleware instance is constructed once at registration time and reused for every request — it has
    /// effectively a singleton lifetime, regardless of how <typeparamref name="TMiddleware"/> itself is
    /// registered with the DI container.
    /// </para>
    /// <para>
    /// Constructor parameters are resolved from the root <see cref="IServiceProvider"/>, not from the
    /// per-request scope. Do NOT inject scoped or transient services (for example, <c>DbContext</c>) via the
    /// constructor — the captured instance will be shared across all requests, which can cause stale data,
    /// thread-safety violations, or disposed-object errors.
    /// </para>
    /// <para>
    /// To consume scoped or transient services, declare them as parameters on <c>InvokeAsync</c> after the
    /// required <see cref="RequestContext{TRequest, TResponse}"/> parameter. Those are resolved from the
    /// per-request scope on every invocation.
    /// </para>
    /// </remarks>
    public RequestHandler<TRequest, TResponse> Use<TMiddleware>(params object[] parameters)
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), serviceProvider, next, parameters)
                .CreateMiddleware());

    /// <summary>
    /// Adds a class-based middleware to the request handler's pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">A class with an InvokeAsync method whose first parameter is <see cref="RequestContext{TRequest, TResponse}"/>.</typeparam>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    /// <remarks>
    /// <para>
    /// The middleware instance is constructed once at registration time and reused for every request — it has
    /// effectively a singleton lifetime, regardless of how <typeparamref name="TMiddleware"/> itself is
    /// registered with the DI container.
    /// </para>
    /// <para>
    /// Constructor parameters are resolved from the root <see cref="IServiceProvider"/>, not from the
    /// per-request scope. Do NOT inject scoped or transient services (for example, <c>DbContext</c>) via the
    /// constructor — the captured instance will be shared across all requests, which can cause stale data,
    /// thread-safety violations, or disposed-object errors.
    /// </para>
    /// <para>
    /// To consume scoped or transient services, declare them as parameters on <c>InvokeAsync</c> after the
    /// required <see cref="RequestContext{TRequest, TResponse}"/> parameter. Those are resolved from the
    /// per-request scope on every invocation.
    /// </para>
    /// </remarks>
    public RequestHandler<TRequest, TResponse> Use<TMiddleware>()
        where TMiddleware : class =>
        Use(next =>
            new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), serviceProvider, next, null)
                .CreateMiddleware());

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, TimeSpan timeout)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        try
        {
            return await InvokeInternalAsync(request, timeoutTokenSource.Token);
        }
        catch (OperationCanceledException ex) when (timeoutTokenSource.IsCancellationRequested)
        {
            throw new TimeoutException($"Pipeline exceeded timeout of {timeout}.", ex);
        }
    }

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutTokenSource.Token);
        try
        {
            return await InvokeInternalAsync(request, linkedTokenSource.Token);
        }
        catch (OperationCanceledException ex)
            when (timeoutTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Pipeline exceeded timeout of {timeout}.", ex);
        }
    }

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, CancellationToken cancellationToken)
    {
        using var serviceScope = serviceProvider.CreateScope();
        var context = new RequestContext<TRequest, TResponse>(
            request,
            Ulid.NewUlid(),
            timeProvider,
            serviceScope.ServiceProvider,
            cancellationToken);

        await handler.Value(context);

        return context.Response;
    }

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

            injectedTypes = [.. allParamTypes.Where(t => t != typeof(RequestContext<TRequest, TResponse>))];

            middleware = (TMiddleware)ActivatorUtilities.CreateInstance(
                services,
                type,
                parameters is null || parameters.Length == 0 ? [next] : [.. parameters.Prepend(next)])
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

                return (Task)(method.Invoke(middleware, args)
                    ?? throw new InvalidOperationException($"{method.DeclaringType?.FullName}.{method.Name} returned null."));
            };
    }

    /// <inheritdoc/>
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP007:Don't dispose injected",
        Justification = "ownership of ConfigurationManager transfers from RequestHandlerBuilder at Build() time")]
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        serviceProvider.Dispose();
        disposed = true;
    }

    private RequestHandler<TRequest, TResponse> ThrowIfDisposed() =>
        disposed ? throw new ObjectDisposedException(GetType().FullName) : this;
}
