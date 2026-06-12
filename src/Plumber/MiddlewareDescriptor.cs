namespace Plumber;

/// <summary>
/// Describes a single middleware registration in a <see cref="RequestHandler{TRequest, TResponse}"/> pipeline.
/// Returned by <see cref="RequestHandler{TRequest, TResponse}.Middleware"/> so tests can assert on pipeline
/// composition without invoking the pipeline.
/// </summary>
/// <param name="MiddlewareType">
/// The middleware class for registrations made via <c>Use&lt;TMiddleware&gt;()</c>;
/// <see langword="null"/> for delegate-based registrations.
/// </param>
/// <param name="DisplayName">
/// A human-readable name for the registration: the middleware type name for class-based registrations,
/// the method name for method-group registrations, and <see cref="DelegateDisplayName"/> for lambdas
/// (whose compiler-generated method names carry no signal).
/// </param>
public sealed record MiddlewareDescriptor(Type? MiddlewareType, string DisplayName)
{
    /// <summary>
    /// The <see cref="DisplayName"/> recorded for lambda registrations. Assert against this constant
    /// to match a delegate slot in the pipeline.
    /// </summary>
    public const string DelegateDisplayName = "<delegate>";
}
