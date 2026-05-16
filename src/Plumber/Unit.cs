namespace Plumber;

/// <summary>
/// The unit type: a type with exactly one value, carrying no information.
/// </summary>
/// <remarks>
/// Borrowed from functional programming (e.g. F# <c>unit</c>, Haskell <c>()</c>), <see cref="Unit"/> is the
/// total-function analogue of <c>void</c>: it lets a pipeline that produces no meaningful response still be
/// typed uniformly as <c>RequestHandler&lt;TRequest, TResponse&gt;</c> rather than requiring a separate
/// void-returning shape. Use it as <c>TResponse</c> for fire-and-forget scenarios such as event handlers
/// (SQS, SNS, EventBridge), queue consumers, and notification dispatchers — for example
/// <c>RequestHandlerBuilder.Create&lt;SQSEvent, Unit&gt;()</c>.
/// </remarks>
public readonly record struct Unit;
