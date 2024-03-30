using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace RequestPipeline;

public interface IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    /// <summary>
    /// this is the place to call IConfigurationBuilder extensions like:
    ///    - AddEnvironmentVariables
    ///    - AddUserSecrets
    ///    - AddJsonFile
    ///    - AddInMemoryCollection
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    IRequestHandlerBuilder<TRequest, TResponse> Configure(Action<IConfigurationBuilder> action);

    IRequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> action);

    [RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue<T>(String, T)")]
    IRequestHandler<TRequest, TResponse> Build();
}
