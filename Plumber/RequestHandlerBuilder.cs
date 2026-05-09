using Microsoft.Extensions.Configuration;

namespace Plumber;

/// <summary>
/// Static factory for <see cref="RequestHandlerBuilder{TRequest, TResponse}"/>.
/// </summary>
public static class RequestHandlerBuilder
{
    /// <summary>
    /// Creates a new request handler builder with no configuration sources registered.
    /// The base path is set to the current working directory; otherwise the configuration is empty.
    /// Chain <see cref="RequestHandlerBuilder{TRequest, TResponse}.AddDefaultConfigurationSources"/> for the standard set, or call individual <c>Add*</c> methods to register sources explicitly.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <returns>A new <see cref="RequestHandlerBuilder{TRequest, TResponse}"/>.</returns>
    public static RequestHandlerBuilder<TRequest, TResponse> Create<TRequest, TResponse>()
        where TRequest : notnull =>
        new([]);

    /// <summary>
    /// Creates a new request handler builder with no configuration sources registered.
    /// The base path is set to the current working directory; <paramref name="args"/> is appended via <c>AddCommandLine</c> last during <see cref="RequestHandlerBuilder{TRequest, TResponse}.Build()"/> so command-line values take precedence over any sources you register.
    /// Chain <see cref="RequestHandlerBuilder{TRequest, TResponse}.AddDefaultConfigurationSources"/> for the standard set, or call individual <c>Add*</c> methods to register sources explicitly.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
    /// <param name="args">Program args passed into <c>Main</c>. Appended to the per-build configuration via <see cref="CommandLineConfigurationExtensions.AddCommandLine(IConfigurationBuilder, string[])"/> at the end of <see cref="RequestHandlerBuilder{TRequest, TResponse}.Build()"/>.</param>
    /// <returns>A new <see cref="RequestHandlerBuilder{TRequest, TResponse}"/>.</returns>
    public static RequestHandlerBuilder<TRequest, TResponse> Create<TRequest, TResponse>(string[] args)
        where TRequest : notnull =>
        new(args);
}
