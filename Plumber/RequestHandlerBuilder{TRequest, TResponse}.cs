using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Plumber;

/// <summary>
/// A builder for request handlers.
/// Configure, register services for, and build <see cref="RequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
/// <typeparam name="TResponse">The type of response handled by the pipeline.</typeparam>
public sealed class RequestHandlerBuilder<TRequest, TResponse>
    where TRequest : notnull
{
    private const string DevEnv = "Development";
    private readonly string[] args;
    private readonly IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory());
    private readonly List<Action<IConfigurationBuilder, string[]>> configureConfiguration = [];
    private readonly List<Action<IServiceCollection, IConfiguration>> configureServices = [];
    private readonly List<Action<ILoggingBuilder>> configureLogging = [];

    // internal prevents unintended construction
    internal RequestHandlerBuilder(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        this.args = args;
    }

    /// <summary>
    /// Adds a configuration source authoring callback. Runs against the per-Build configuration builder during <see cref="Build(TimeSpan)"/>, before <c>AddCommandLine</c> is appended.
    /// </summary>
    /// <param name="configure">Callback receiving the per-Build <see cref="IConfigurationBuilder"/> and the original <c>args</c> array.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> ConfigureConfiguration(Action<IConfigurationBuilder, string[]> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configureConfiguration.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds a service registration callback. Runs during <see cref="Build(TimeSpan)"/> with the built <see cref="IConfiguration"/> available for binding options or selecting registrations.
    /// </summary>
    /// <param name="configure">Callback receiving the <see cref="IServiceCollection"/> and the built <see cref="IConfiguration"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> ConfigureServices(Action<IServiceCollection, IConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configureServices.Add(configure);
        return this;
    }

    /// <summary>
    /// Adds a logging configuration callback. Runs inside <see cref="LoggingServiceCollectionExtensions.AddLogging(IServiceCollection, Action{ILoggingBuilder})"/> during <see cref="Build(TimeSpan)"/>. Logging infrastructure is only registered when at least one logging callback is present.
    /// </summary>
    /// <param name="configure">Callback receiving the <see cref="ILoggingBuilder"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configureLogging.Add(configure);
        return this;
    }

    /// <summary>
    /// Builds a <see cref="RequestHandler{TRequest, TResponse}"/> from the current recipe.
    /// May be called multiple times — each call produces an independent handler with its own service provider and configuration root, both disposed when the handler is disposed.
    /// </summary>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    public RequestHandler<TRequest, TResponse> Build() =>
        Build(Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Adds a JSON configuration file to the builder. See <see cref="JsonConfigurationExtensions.AddJsonFile(IConfigurationBuilder, string, bool)"/>.
    /// </summary>
    /// <param name="path">Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties"/>.</param>
    /// <param name="optional">If <c>true</c>, the file is optional; otherwise the file must exist.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddJsonFile(string path, bool optional)
    {
        _ = configurationBuilder.AddJsonFile(path, optional);
        return this;
    }

    /// <summary>
    /// Adds a JSON configuration file with reload-on-change support. See <see cref="JsonConfigurationExtensions.AddJsonFile(IConfigurationBuilder, string, bool, bool)"/>.
    /// </summary>
    /// <param name="path">Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties"/>.</param>
    /// <param name="optional">If <c>true</c>, the file is optional; otherwise the file must exist.</param>
    /// <param name="reloadOnChange">If <c>true</c>, the file is watched and configuration is reloaded when it changes.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddJsonFile(string path, bool optional, bool reloadOnChange)
    {
        _ = configurationBuilder.AddJsonFile(path, optional, reloadOnChange);
        return this;
    }

    /// <summary>
    /// Adds an in-memory key/value source to the configuration. See <see cref="MemoryConfigurationBuilderExtensions.AddInMemoryCollection(IConfigurationBuilder, IEnumerable{KeyValuePair{string, string}})"/>.
    /// </summary>
    /// <param name="initialData">Key/value pairs to seed the in-memory source. Pass <c>null</c> for an empty source.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddInMemoryCollection(IEnumerable<KeyValuePair<string, string?>>? initialData = null)
    {
        _ = configurationBuilder.AddInMemoryCollection(initialData);
        return this;
    }

    /// <summary>
    /// Adds environment variables (no prefix) to the configuration. See <see cref="EnvironmentVariablesExtensions.AddEnvironmentVariables(IConfigurationBuilder)"/>.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables()
    {
        _ = configurationBuilder.AddEnvironmentVariables();
        return this;
    }

    /// <summary>
    /// Adds environment variables with the specified prefix to the configuration. The prefix is stripped from each key. See <see cref="EnvironmentVariablesExtensions.AddEnvironmentVariables(IConfigurationBuilder, string)"/>.
    /// </summary>
    /// <param name="prefix">Prefix that environment variables must start with to be included.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddEnvironmentVariables(string prefix)
    {
        _ = configurationBuilder.AddEnvironmentVariables(prefix);
        return this;
    }

    /// <summary>
    /// Sets the base path that file-based configuration providers resolve relative paths against. See <see cref="FileConfigurationExtensions.SetBasePath(IConfigurationBuilder, string)"/>.
    /// </summary>
    /// <param name="path">Absolute path used as the base for file providers.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> SetBasePath(string path)
    {
        _ = configurationBuilder.SetBasePath(path);
        return this;
    }

    /// <summary>
    /// Adds user secrets identified by an explicit secrets id. See <see cref="UserSecretsConfigurationExtensions.AddUserSecrets(IConfigurationBuilder, string, bool)"/>.
    /// </summary>
    /// <param name="secretsid">The user-secrets identifier (matches the <c>UserSecretsId</c> MSBuild property of the source project).</param>
    /// <param name="optional">If <c>true</c>, missing secrets storage is allowed; otherwise an exception is thrown.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets(string secretsid, bool optional)
    {
        _ = configurationBuilder.AddUserSecrets(secretsid, optional);
        return this;
    }

    /// <summary>
    /// Adds user secrets discovered via the <see cref="Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute"/> on the assembly containing <typeparamref name="T"/>. See <see cref="UserSecretsConfigurationExtensions.AddUserSecrets{T}(IConfigurationBuilder)"/>.
    /// </summary>
    /// <typeparam name="T">A type from the consumer's assembly. The assembly must declare <c>UserSecretsId</c> in its csproj.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets<T>()
        where T : class
    {
        _ = configurationBuilder.AddUserSecrets<T>();
        return this;
    }

    /// <summary>
    /// Adds user secrets discovered via the <see cref="Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute"/> on the assembly containing <typeparamref name="T"/>. See <see cref="UserSecretsConfigurationExtensions.AddUserSecrets{T}(IConfigurationBuilder, bool)"/>.
    /// </summary>
    /// <typeparam name="T">A type from the consumer's assembly. The assembly must declare <c>UserSecretsId</c> in its csproj.</typeparam>
    /// <param name="optional">If <c>true</c>, missing secrets storage is allowed; otherwise an exception is thrown.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets<T>(bool optional)
        where T : class
    {
        _ = configurationBuilder.AddUserSecrets<T>(optional);
        return this;
    }

    /// <summary>
    /// Adds user secrets with reload-on-change support. See <see cref="UserSecretsConfigurationExtensions.AddUserSecrets{T}(IConfigurationBuilder, bool, bool)"/>.
    /// </summary>
    /// <typeparam name="T">A type from the consumer's assembly. The assembly must declare <c>UserSecretsId</c> in its csproj.</typeparam>
    /// <param name="optional">If <c>true</c>, missing secrets storage is allowed; otherwise an exception is thrown.</param>
    /// <param name="reloadOnChange">If <c>true</c>, the secrets file is watched and configuration is reloaded when it changes.</param>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddUserSecrets<T>(bool optional, bool reloadOnChange)
        where T : class
    {
        _ = configurationBuilder.AddUserSecrets<T>(optional, reloadOnChange);
        return this;
    }

    /// <summary>
    /// Adds the standard set of default configuration sources: <c>appsettings.json</c>, <c>appsettings.{ENV}.json</c>, <c>DOTNET_</c>-prefixed environment variables, and all environment variables.
    /// Command-line args are appended automatically by <see cref="Build(TimeSpan)"/> so they always take precedence.
    /// User secrets are not included — call <see cref="AddUserSecrets{T}()"/> explicitly with a type from your assembly.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public RequestHandlerBuilder<TRequest, TResponse> AddDefaultConfigurationSources()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? DevEnv;
        _ = configurationBuilder
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("DOTNET_")
            .AddEnvironmentVariables();

        return this;
    }

    /// <summary>
    /// Builds a <see cref="RequestHandler{TRequest, TResponse}"/> from the current recipe with a custom request timeout.
    /// May be called multiple times — each call produces an independent handler with its own service provider and configuration root.
    /// </summary>
    /// <param name="timeout">The timeout applied to each request invocation.</param>
    /// <returns><see cref="RequestHandler{TRequest, TResponse}"/></returns>
    public RequestHandler<TRequest, TResponse> Build(TimeSpan timeout)
    {
        var perBuild = CreatePerBuildConfigurationBuilder();
        ApplyConfigurationCallbacks(perBuild);
        // command-line args last so they take precedence over user-supplied sources
        _ = perBuild.AddCommandLine(args);

        var configuration = perBuild.Build();
        var serviceCollection = new ServiceCollection();
        // factory registration so DI captures the IConfigurationRoot for disposal
        _ = serviceCollection.AddSingleton<IConfiguration>(_ => configuration);

        ApplyLoggingCallbacks(serviceCollection);
        ApplyServiceCallbacks(serviceCollection, configuration);

        return new RequestHandler<TRequest, TResponse>(serviceCollection, timeout);
    }

    // Shallow-copies sources and properties from the shared configurationBuilder into a fresh
    // ConfigurationBuilder so that callbacks and AddCommandLine don't accumulate on the shared
    // field across multiple Build() calls. Source instances are reused; each Build still produces
    // a distinct IConfigurationRoot with its own provider instances.
    private ConfigurationBuilder CreatePerBuildConfigurationBuilder()
    {
        var perBuild = new ConfigurationBuilder();
        foreach (var src in configurationBuilder.Sources)
        {
            perBuild.Sources.Add(src);
        }

        foreach (var kv in configurationBuilder.Properties)
        {
            perBuild.Properties[kv.Key] = kv.Value;
        }

        return perBuild;
    }

    private void ApplyConfigurationCallbacks(IConfigurationBuilder perBuild)
    {
        foreach (var c in configureConfiguration)
        {
            c(perBuild, args);
        }
    }

    private void ApplyLoggingCallbacks(IServiceCollection serviceCollection)
    {
        if (configureLogging.Count == 0)
        {
            return;
        }

        _ = serviceCollection.AddLogging(b =>
        {
            foreach (var c in configureLogging)
            {
                c(b);
            }
        });
    }

    private void ApplyServiceCallbacks(IServiceCollection serviceCollection, IConfiguration configuration)
    {
        foreach (var c in configureServices)
        {
            c(serviceCollection, configuration);
        }
    }
}
