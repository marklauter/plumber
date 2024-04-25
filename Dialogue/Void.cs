namespace Dialogue;

/// <summary>
/// A type that represents the absence of a response type. 
/// </summary>
/// <remarks>
/// This type is useful for the TResponse generic parameter when the request scenario doesn't require a response.
/// Examples include IRequestHandlerBuilder{string, Void}, RequestContext{string, Void}, etc.
/// </remarks>
public readonly struct Void { }

