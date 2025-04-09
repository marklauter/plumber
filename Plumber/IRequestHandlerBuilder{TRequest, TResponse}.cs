using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Plumber;

/// <summary>
/// A builder for request handlers.
/// Configure, register services for, and build <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public interface IRequestHandlerBuilder<TRequest, TResponse>
    : ILoggingBuilder
    , IMetricsBuilder
    where TRequest : notnull
{
    /// <summary>
    /// The <see cref="IConfigurationManager"/> for the request handler.
    /// </summary>
    /// <remarks><seealso href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0"/></remarks>
    IConfigurationManager Configuration { get; }

    /// <summary>
    /// The <see cref="IServiceCollection"/> for the request handler.
    /// </summary>
    new IServiceCollection Services { get; }

    /// <summary>
    /// Call Build to create an instance of <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <returns><see cref="IRequestHandler{TRequest, TResponse}"/></returns>
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    IRequestHandler<TRequest, TResponse> Build();
}
