namespace Dialogue;

/// <summary>
/// A type that represents the absence of a response type. Useful type for TResponse generic param when request scenario doesn't require a response.
/// Examples: IRequestHandlerBuilder{string, Void}, RequestContext{string, Void}, etc.
/// </summary>
public readonly struct Void { }

