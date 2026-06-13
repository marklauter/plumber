using Microsoft.Extensions.DependencyInjection;

namespace Plumber.Diagnostics.Tests;

internal sealed class TestRequest
{
    public string Value { get; init; } = string.Empty;
}

internal sealed class TestResponse
{
    public bool Success { get; init; }
}

internal static class TestPipeline
{
    /// <summary>
    /// A terminal middleware factory that assigns a successful response and completes the pipeline.
    /// </summary>
    public static Func<RequestMiddleware<TestRequest, TestResponse>, RequestMiddleware<TestRequest, TestResponse>> Succeeds { get; } =
        next => context =>
        {
            context.Response = new TestResponse { Success = true };
            return next(context);
        };

    /// <summary>
    /// Builds a request handler for the test request/response pair. The default options infrastructure is
    /// always registered so the parameterless <c>UseRequestTracing</c>, <c>UseRequestMetrics</c>, and
    /// <c>UseRequestDiagnostics</c> overloads can resolve their <c>IOptions</c> dependency; tests can layer
    /// further registration (e.g. configured <c>AddPlumberDiagnostics</c>) through <paramref name="configureServices"/>.
    /// </summary>
    public static RequestHandler<TestRequest, TestResponse> CreateHandler(Action<IServiceCollection>? configureServices = null) =>
        RequestHandlerBuilder.Create<TestRequest, TestResponse>()
            .ConfigureServices((services, configuration) =>
            {
                _ = services.AddPlumberDiagnostics<TestRequest, TestResponse>();
                configureServices?.Invoke(services);
            })
            .Build();
}
