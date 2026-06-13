using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Plumber;

/// <summary>
/// Setup and invoke the request/response pipeline. Obtained from <see cref="RequestHandlerBuilder{TRequest, TResponse}.Build()"/>; not constructable directly.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public sealed class RequestHandler<TRequest, TResponse>
    : IDisposable
    , IAsyncDisposable
    where TRequest : notnull
{
    private readonly List<Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>>> components = [];
    private readonly List<MiddlewareDescriptor> descriptors = [];
    // internal (not private) so Plumber.Testing (InternalsVisibleTo) can surface the root provider
    // for test assertions; production consumers resolve services through RequestContext.Services,
    // which is scoped per request
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP008:Don't assign member with injected and created disposables", Justification = "Ownership is tracked via ownsProvider; only owned providers are disposed in Dispose().")]
    internal IServiceProvider Services { get; }
    private readonly bool ownsProvider;
    private readonly TimeProvider timeProvider;
    private readonly Lazy<RequestMiddleware<TRequest, TResponse>> handler;
    private bool disposed;

    internal RequestHandler(
        ServiceCollection serviceCollection,
        TimeSpan timeout)
    {
        Services = serviceCollection.BuildServiceProvider();
        ownsProvider = true;
        try
        {
            timeProvider = Services.GetRequiredService<TimeProvider>();
        }
        catch
        {
            // We own the provider, but no instance escapes the ctor to call Dispose, so tear it down here.
            // Sync Dispose is correct: an async-only singleton could only exist if the throwing resolution
            // had already created one, which is pathological.
            (Services as IDisposable)?.Dispose();
            throw;
        }

        handler = new Lazy<RequestMiddleware<TRequest, TResponse>>(BuildPipeline);
        Timeout = timeout;
    }

    internal RequestHandler(
        IServiceProvider services,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.GetService<IServiceScopeFactory>() is null)
        {
            throw new InvalidOperationException(
                $"The injected {nameof(IServiceProvider)} must support {nameof(IServiceScopeFactory)} (typically obtained from {nameof(ServiceCollection)}.{nameof(ServiceCollectionContainerBuilderExtensions.BuildServiceProvider)} or a host-built provider).");
        }

        Services = services;
        ownsProvider = false;
        timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;
        handler = new Lazy<RequestMiddleware<TRequest, TResponse>>(BuildPipeline);
        Timeout = timeout;
    }

    /// <summary>
    /// The timeout for the request handler's pipeline.
    /// </summary>
    /// <remarks>
    /// When Timeout is set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> <see cref="CancellationToken.None"/> is passed to the <see cref="RequestContext{TRequest, TResponse}"/> constructor.
    /// Otherwise, a timeout-based <see cref="CancellationTokenSource"/> is used to provide the cancellation token for the request context.
    /// When the timeout elapses before the pipeline completes, <see cref="InvokeAsync(TRequest)"/> throws <see cref="TimeoutException"/> rather than <see cref="OperationCanceledException"/>, so timeouts can be distinguished from caller-initiated cancellation.
    /// The registered <see cref="TimeProvider"/> drives the timeout timer; supplying a custom provider (for example, <c>FakeTimeProvider</c>) controls when the timeout fires.
    /// </remarks>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// The pipeline's middleware registrations, in registration order.
    /// </summary>
    /// <remarks>
    /// Registration order is execution order on the way in: <c>Middleware[0]</c> is the outermost middleware
    /// and sees the request first. Class-based registrations (<see cref="Use{TMiddleware}()"/>) carry the
    /// middleware type in <see cref="MiddlewareDescriptor.MiddlewareType"/>; delegate-based registrations have a
    /// <see langword="null"/> type and a <see cref="MiddlewareDescriptor.DisplayName"/> of the method name
    /// (method groups) or <see cref="MiddlewareDescriptor.DelegateDisplayName"/> (lambdas).
    /// Intended for asserting on pipeline composition in tests — it exposes registration metadata only,
    /// never the component delegates or the compiled pipeline. The list remains readable after the pipeline
    /// is built on the first <see cref="InvokeAsync(TRequest)"/>.
    /// </remarks>
    public IReadOnlyList<MiddlewareDescriptor> Middleware => descriptors.AsReadOnly();

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request">The request value flowed through the pipeline as <see cref="RequestContext{TRequest, TResponse}.Request"/>.</param>
    /// <returns>A task that completes with the value of <see cref="RequestContext{TRequest, TResponse}.Response"/> after the pipeline returns; <see langword="null"/> if no middleware assigned <c>Response</c>.</returns>
    /// <remarks>
    /// Each invocation creates a new DI scope; <see cref="RequestContext{TRequest, TResponse}.Services"/> is the per-request scoped provider and is disposed when the pipeline returns.
    /// Consumers of <c>RequestContext.Services</c> do not need to call <c>CreateScope()</c>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">Thrown when <see cref="Timeout"/> elapses before the pipeline completes.</exception>
    public Task<TResponse?> InvokeAsync(TRequest request)
    {
        ThrowIfRequestNull(request);
        return ThrowIfDisposed()
            .Timeout == System.Threading.Timeout.InfiniteTimeSpan
                ? InvokeInternalAsync(request, CancellationToken.None)
                : InvokeInternalAsync(request, Timeout);
    }

    /// <summary>
    /// Invokes the request handler's pipeline.
    /// </summary>
    /// <param name="request">The request value flowed through the pipeline as <see cref="RequestContext{TRequest, TResponse}.Request"/>.</param>
    /// <param name="cancellationToken">Caller-supplied cancellation token. Linked with the internal timeout source when <see cref="Timeout"/> is finite.</param>
    /// <returns>A task that completes with the value of <see cref="RequestContext{TRequest, TResponse}.Response"/> after the pipeline returns; <see langword="null"/> if no middleware assigned <c>Response</c>.</returns>
    /// <remarks>
    /// Each invocation creates a new DI scope; <see cref="RequestContext{TRequest, TResponse}.Services"/> is the per-request scoped provider and is disposed when the pipeline returns.
    /// Consumers of <c>RequestContext.Services</c> do not need to call <c>CreateScope()</c>.
    /// When both <paramref name="cancellationToken"/> and the internal timeout fire, caller cancellation wins (an <see cref="OperationCanceledException"/> propagates rather than a <see cref="TimeoutException"/>).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">Thrown when <see cref="Timeout"/> elapses before the pipeline completes and <paramref name="cancellationToken"/> was not cancelled.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<TResponse?> InvokeAsync(TRequest request, CancellationToken cancellationToken)
    {
        ThrowIfRequestNull(request);
        return ThrowIfDisposed()
            .Timeout == System.Threading.Timeout.InfiniteTimeSpan
                ? InvokeInternalAsync(request, cancellationToken)
                : InvokeInternalAsync(request, Timeout, cancellationToken);
    }

    // ArgumentNullException.ThrowIfNull(object?) boxes a value-type TRequest on every invocation.
    // 'is null' is compile-time false for value types (the JIT elides the branch) and a real null
    // check for reference types, so the guard costs nothing on the value-type hot path.
    private static void ThrowIfRequestNull(TRequest request)
    {
#pragma warning disable CA1510 // ThrowIfNull boxes value-type TRequest; the 'is null' form avoids it
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
#pragma warning restore CA1510
    }

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware">A delegate that receives the next middleware in the chain and returns a wrapped <see cref="RequestMiddleware{TRequest, TResponse}"/>.</param>
    /// <returns>This <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="middleware"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    public RequestHandler<TRequest, TResponse> Use(Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        return Use(middleware, new MiddlewareDescriptor(null, DelegateDisplayName(middleware)));
    }

    /// <summary>
    /// Adds a middleware to the request handler's pipeline.
    /// </summary>
    /// <param name="middleware">An async delegate receiving the <see cref="RequestContext{TRequest, TResponse}"/> and the next middleware delegate; returns a <see cref="Task"/> that completes when this middleware (and any downstream middleware it awaits) finishes.</param>
    /// <returns>This <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="middleware"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    public RequestHandler<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        return Use(next => context => middleware(context, next), new MiddlewareDescriptor(null, DelegateDisplayName(middleware)));
    }

    /// <summary>
    /// Adds a class-based middleware to the request handler's pipeline with constructor parameters.
    /// </summary>
    /// <typeparam name="TMiddleware">A class with an InvokeAsync method whose first parameter is <see cref="RequestContext{TRequest, TResponse}"/>.</typeparam>
    /// <param name="parameters">Constructor arguments for the middleware implementation.</param>
    /// <returns>This <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    /// <remarks>
    /// <para>
    /// Constructor arguments are always passed after the Next middleware argument and before arguments provided by the service provider.
    /// </para>
    /// <para>
    /// The middleware's <c>InvokeAsync</c> shape is validated when <c>Use</c> is called; the instance is
    /// constructed once when the pipeline is built (on the first <see cref="InvokeAsync(TRequest)"/>) and
    /// reused for every request — it has effectively a singleton lifetime, regardless of how
    /// <typeparamref name="TMiddleware"/> itself is registered with the DI container.
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
        where TMiddleware : class
    {
        // validate the middleware shape now so misconfiguration fails here, not on first InvokeAsync
        var (method, methodParams) = MiddlewareFactory<TMiddleware>.ValidateInvokeMethod();
        return Use(
            next => new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), Services, next, parameters, method, methodParams)
                .CreateMiddleware(),
            new MiddlewareDescriptor(typeof(TMiddleware), typeof(TMiddleware).Name));
    }

    /// <summary>
    /// Adds a class-based middleware to the request handler's pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">A class with an InvokeAsync method whose first parameter is <see cref="RequestContext{TRequest, TResponse}"/>.</typeparam>
    /// <returns>This <see cref="RequestHandler{TRequest, TResponse}"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">New middleware components can't be added after the pipeline has been built. The pipeline is built on the first call to InvokeAsync.</exception>
    /// <remarks>
    /// <para>
    /// The middleware's <c>InvokeAsync</c> shape is validated when <c>Use</c> is called; the instance is
    /// constructed once when the pipeline is built (on the first <see cref="InvokeAsync(TRequest)"/>) and
    /// reused for every request — it has effectively a singleton lifetime, regardless of how
    /// <typeparamref name="TMiddleware"/> itself is registered with the DI container.
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
        where TMiddleware : class
    {
        // validate the middleware shape now so misconfiguration fails here, not on first InvokeAsync
        var (method, methodParams) = MiddlewareFactory<TMiddleware>.ValidateInvokeMethod();
        return Use(
            next => new MiddlewareFactory<TMiddleware>(typeof(TMiddleware), Services, next, null, method, methodParams)
                .CreateMiddleware(),
            new MiddlewareDescriptor(typeof(TMiddleware), typeof(TMiddleware).Name));
    }

    // '<' cannot appear in a C# identifier, so its presence marks a compiler-generated lambda method
    private static string DelegateDisplayName(Delegate middleware) =>
        middleware.Method.Name.Contains('<', StringComparison.Ordinal)
            ? MiddlewareDescriptor.DelegateDisplayName
            : middleware.Method.Name;

    private RequestHandler<TRequest, TResponse> Use(
        Func<RequestMiddleware<TRequest, TResponse>, RequestMiddleware<TRequest, TResponse>> middleware,
        MiddlewareDescriptor descriptor)
    {
        if (handler.IsValueCreated)
        {
            throw new InvalidOperationException("middleware components cannot be added after the pipeline has been built.");
        }

        ThrowIfDisposed().components.Add(middleware);
        descriptors.Add(descriptor);
        return this;
    }

    private async Task<TResponse?> InvokeInternalAsync(TRequest request, TimeSpan timeout)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout, timeProvider);
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
        using var timeoutTokenSource = new CancellationTokenSource(timeout, timeProvider);
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
        await using var serviceScope = Services.CreateAsyncScope();
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

        // Eager shape validation, called from Use<TMiddleware>() at registration so a misconfigured
        // middleware fails at the call site rather than on the first InvokeAsync. Pure reflection over
        // TMiddleware — no 'next', no service provider, no instance. Mirrors ASP.NET Core's UseMiddleware,
        // which validates the method shape eagerly and defers only ActivatorUtilities.CreateInstance.
        public static (MethodInfo Method, ParameterInfo[] Parameters) ValidateInvokeMethod()
        {
            var candidates = typeof(TMiddleware)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == InvokeMethodName)
                .ToArray();

            var method = candidates.Length switch
            {
                0 => throw new InvalidOperationException($"{InvokeMethodName} method not found on class {typeof(TMiddleware).FullName}."),
                1 => candidates[0],
                _ => throw new InvalidOperationException($"class {typeof(TMiddleware).FullName} declares multiple {InvokeMethodName} methods; the convention requires exactly one."),
            };

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new InvalidOperationException($"{InvokeMethodName} must return {nameof(Task)}");
            }

            var methodParams = method.GetParameters();

            return methodParams.Length == 0 || methodParams[0].ParameterType != ContextType
                ? throw new InvalidOperationException($"method {method.Name} must have {ContextType.Name} as its first parameter")
                : (method, methodParams);
        }

        // Deferred construction, run when the pipeline is built (first InvokeAsync). Consumes the
        // method validated eagerly by ValidateInvokeMethod; only instantiation needs 'next' and the provider.
        public MiddlewareFactory(
            Type type,
            IServiceProvider services,
            RequestMiddleware<TRequest, TResponse> next,
            object[]? parameters,
            MethodInfo method,
            ParameterInfo[] methodParams)
        {
            var middleware = (TMiddleware)ActivatorUtilities.CreateInstance(
                services,
                type,
                parameters is null || parameters.Length == 0 ? [next] : [.. parameters.Prepend(next)]);

            handler = Compile(middleware, method, methodParams);
        }

        private readonly RequestMiddleware<TRequest, TResponse> handler;

        public RequestMiddleware<TRequest, TResponse> CreateMiddleware() => handler;

        private static RequestMiddleware<TRequest, TResponse> Compile(
            TMiddleware middleware,
            MethodInfo method,
            ParameterInfo[] methodParams)
        {
            var contextParam = Expression.Parameter(ContextType, "context");
            var servicesProp = Expression.Property(contextParam, nameof(RequestContext<,>.Services));

            var callArgs = new Expression[methodParams.Length];
            callArgs[0] = contextParam;
            for (var i = 1; i < methodParams.Length; ++i)
            {
                callArgs[i] = Expression.Call(
                    typeof(ServiceProviderServiceExtensions),
                    nameof(ServiceProviderServiceExtensions.GetRequiredService),
                    [methodParams[i].ParameterType],
                    servicesProp);
            }

            Expression call = Expression.Call(Expression.Constant(middleware), method, callArgs);
            if (call.Type != typeof(Task))
            {
                call = Expression.Convert(call, typeof(Task));
            }

            // preserve the "returned null" diagnostic from the previous reflection-based dispatch
            var nullMessage = $"{method.DeclaringType?.FullName}.{method.Name} returned null.";
            var throwOnNull = Expression.Throw(
                Expression.New(
                    typeof(InvalidOperationException).GetConstructor([typeof(string)])!,
                    Expression.Constant(nullMessage)),
                typeof(Task));
            var body = Expression.Coalesce(call, throwOnNull);

            return Expression.Lambda<RequestMiddleware<TRequest, TResponse>>(body, contextParam).Compile();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Prefer <see cref="DisposeAsync"/> when any registered singleton implements only <see cref="IAsyncDisposable"/> —
    /// the underlying service provider's synchronous <c>Dispose</c> throws <see cref="InvalidOperationException"/> for such services.
    /// </remarks>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (ownsProvider)
        {
            (Services as IDisposable)?.Dispose();
        }

        disposed = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Disposes the owned service provider asynchronously so singletons implementing only
    /// <see cref="IAsyncDisposable"/> are disposed correctly. Injected providers are never disposed.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        if (ownsProvider)
        {
            // the owned provider always comes from ServiceCollection.BuildServiceProvider, which is IAsyncDisposable
            await ((IAsyncDisposable)Services).DisposeAsync();
        }

        disposed = true;
    }

    private RequestHandler<TRequest, TResponse> ThrowIfDisposed() =>
        disposed ? throw new ObjectDisposedException(GetType().FullName) : this;
}
