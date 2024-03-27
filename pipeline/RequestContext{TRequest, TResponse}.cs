namespace Pipeline.Tests;

public record RequestContext<TRequest, TResponse>(
    TRequest Request,
    IServiceProvider Services,
    CancellationToken CancellationToken)
    where TRequest : class
    where TResponse : class
{
    public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();
    public TResponse? Response { get; set; }
}