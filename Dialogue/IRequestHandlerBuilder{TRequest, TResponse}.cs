using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Dialogue;

/// <summary>
/// A builder for request handlers.
/// Configure, register services for, and build <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public interface IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// Call Configure to setup the configuration for the request handler.
    /// This is the place to call IConfigurationBuilder extensions like:
    ///    - AddEnvironmentVariables
    ///    - AddUserSecrets
    ///    - AddJsonFile
    ///    - AddInMemoryCollection
    /// </summary>
    /// <param name="action"><see cref="Action{T}"/> is a user provided action that receives <see cref="IConfigurationBuilder"/>.</param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    IRequestHandlerBuilder<TRequest, TResponse> Configure(Action<IConfigurationBuilder> action);

    /// <summary>
    /// Call ConfigureServices to register services with the <see cref="IServiceCollection"/> for the request handler.
    /// </summary>
    /// <param name="action"><see cref="Action{T1, T2}"/>is a user provided action that receives <see cref="IServiceCollection"/> and <see cref="IConfiguration"/>.</param>
    /// <returns><see cref="IRequestHandlerBuilder{TRequest, TResponse}"/></returns>
    IRequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> action);

    /// <summary>
    /// Call Build to create <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    /// <returns></returns>
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    IRequestHandler<TRequest, TResponse> Build();
}
