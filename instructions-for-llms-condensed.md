# Plumber Pipeline: Condensed Guide for AI Agents

## Core Concepts

- **What**: A middleware pipeline framework for C# .NET 8 applications without built-in hosts
- **Purpose**: Implements request-response pattern with middleware, DI, and configuration support
- **Key Types**:
  - `RequestHandlerBuilder<TReq, TRes>`: Creates and configures a request handler
  - `IRequestHandler<TReq, TRes>`: The pipeline that processes requests
  - `RequestContext<TReq, TRes>`: Contains request, response, and contextual data
  - `RequestMiddleware<TReq, TRes>`: Represents a middleware component
  - `Void`: Special type for pipelines without responses

## Quick Implementation Pattern

```csharp
// 1. Create builder
var builder = RequestHandlerBuilder.Create<ApiRequest, ApiResponse>();

// 2. Configure services & settings
builder.Services.AddScoped<IMyService, MyService>();
builder.Configuration.AddJsonFile("settings.json", optional: true);

// 3. Build handler and add middleware
var handler = builder.Build()
    .Use<LoggingMiddleware>()
    .Use<ValidationMiddleware>()
    .Use<ProcessingMiddleware>();

// 4. Invoke pipeline
var response = await handler.InvokeAsync(request);
```

## Middleware Implementation Options

### 1. Delegate Middleware (Simple)

```csharp
handler.Use(async (context, next) => {
    // Pre-processing
    Console.WriteLine($"Processing request {context.Id}");
    
    // Call next middleware
    await next(context);
    
    // Post-processing
    Console.WriteLine($"Completed request {context.Id}");
});
```

### 2. Class-Based Middleware (Complex Logic)

```csharp
public sealed class ValidationMiddleware(
    RequestMiddleware<ApiRequest, ApiResponse> next,
    IValidator<ApiRequest> validator)
{
    public async Task InvokeAsync(RequestContext<ApiRequest, ApiResponse> context)
    {
        // Check cancellation
        context.CancellationToken.ThrowIfCancellationRequested();
        
        // Pre-processing
        var validationResult = await validator.ValidateAsync(context.Request);
        
        if (!validationResult.IsValid)
        {
            // Short-circuit with error response
            context.Response = new ApiResponse {
                Success = false,
                ErrorCode = "VALIDATION_ERROR"
            };
            return; // Stop pipeline execution
        }
        
        // Continue pipeline
        await next(context);
    }
}
```

## Key Patterns to Know

### 1. Sharing Data Between Middleware

```csharp
// First middleware sets data
context.Data["AuthUser"] = user;

// Later middleware uses data
if (context.TryGetValue<User>("AuthUser", out var user))
{
    // Use user object
}
```

### 2. Short-Circuit Pattern

Stop pipeline execution early by not calling `next(context)` and setting a response:

```csharp
if (!isValid) {
    context.Response = new ApiResponse { Success = false };
    return; // Short-circuit here
}

// Only reaches here if valid
await next(context);
```

### 3. Error Handling Strategy

```csharp
public sealed class ErrorHandlingMiddleware(
    RequestMiddleware<ApiRequest, ApiResponse> next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(RequestContext<ApiRequest, ApiResponse> context)
    {
        try {
            await next(context);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Request {RequestId} failed", context.Id);
            context.Response = new ApiResponse {
                Success = false,
                ErrorCode = "INTERNAL_ERROR",
                Error = ex.Message
            };
        }
    }
}
```

## Best Practices

1. **Place Error Handling Early**: Add error middleware at the beginning to catch all exceptions

2. **Use Cancellation Tokens**: Check `context.CancellationToken.ThrowIfCancellationRequested()`

3. **Key Order Awareness**: Code before `await next()` runs in registration order, code after runs in reverse order

4. **Use Type-Safe Data Retrieval**: Always use `context.TryGetValue<T>()` with constants for keys

5. **Operation-Specific Timeouts**:
   ```csharp
   using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
   using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
       context.CancellationToken, timeoutSource.Token);
   
   // Use linkedSource.Token for operations
   ```

## Key Use Cases

### AWS Lambda Functions
```csharp
public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
{
    await handler.InvokeAsync(sqsEvent, context.CancellationToken);
}
```

### Console Applications
```csharp
public static async Task Main(string[] args)
{
    var result = await handler.InvokeAsync(options);
    Environment.ExitCode = result.Success ? 0 : 1;
}
```

### Message Queue Processors
```csharp
public async Task ProcessMessageAsync(Message message)
{
    await handler.InvokeAsync(message);
}
```

## Common Pitfalls

1. Forgetting to call `await next(context)` (unless short-circuiting)
2. Not checking cancellation tokens
3. Adding middleware after the first `InvokeAsync` call
4. Using incorrect service lifetimes
