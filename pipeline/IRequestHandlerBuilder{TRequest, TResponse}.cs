using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Pipeline.Tests;

public interface IRequestHandlerBuilder<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{

    RequestHandlerBuilder<TRequest, TResponse> BuildConfiguration(Action<IConfigurationBuilder> action);
    RequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> action);

    RequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables();
    RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(bool optional, bool reloadOnChange);
    RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(string userSecretId, bool reloadOnChange);
    RequestHandlerBuilder<TRequest, TResponse> AddCommandLineArgs(string[] args);
    RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange);
    RequestHandlerBuilder<TRequest, TResponse> AddAppSettingsJsonFile(bool optional, bool reloadOnChange, string environment);
    RequestHandlerBuilder<TRequest, TResponse> AddInMemoryCollection(IDictionary<string, string?> options);

    RequestHandler<TRequest, TResponse> Build();
    RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestContext<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>, Task> middleware);
    RequestHandlerBuilder<TRequest, TResponse> Use(Func<RequestDelegate<TRequest, TResponse>, RequestDelegate<TRequest, TResponse>> middleware);
    RequestHandlerBuilder<TRequest, TResponse> Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>() where TMiddleware : class, IMiddleware<TRequest, TResponse>;
}