# Timeout cancellation indistinguishable from user cancellation

- **Area:** RequestHandler (timeout handling)
- **Priority:** Medium
- **Status:** Resolved

## Problem
When a timeout occurs, the linked `CancellationToken` is cancelled, producing `OperationCanceledException`. This is indistinguishable from user-initiated cancellation. For AWS Lambda scenarios, distinguishing between internal timeouts and external cancellations is important for debugging.

## Suggested Fix
Catch `OperationCanceledException` and check if the timeout token triggered, then throw `TimeoutException` with a descriptive message.

## Code References
- `Plumber/RequestHandler{TRequest, TResponse}.cs:62-75` — `InvokeInternalAsync` overloads with timeout

## Notes
None.
