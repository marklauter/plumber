---
title: Architecture — non-obvious invariants
summary: Design invariants of the Plumber pipeline that the code leaves implicit.
tags: [agent-guidance, architecture]
created: 2026-05-10
---

# Architecture — non-obvious invariants

- `RequestHandlerBuilder<TReq, TRes>` is callback-based: `ConfigureServices`/`ConfigureLogging`/`ConfigureConfiguration` queue actions that run during `Build()`. It does not implement `ILoggingBuilder`/`IMetricsBuilder` and exposes no `Services` or `Configuration` properties.
- `IConfiguration` is registered via factory during `Build()`, so the DI container owns its lifetime. Handler `Dispose`/`DisposeAsync` only disposes the owned service provider (injected providers are never disposed); configuration is disposed transitively. `DisposeAsync` is required when async-only-disposable singletons are registered; the per-request scope is always disposed asynchronously.
- The pipeline is built lazily on first `InvokeAsync()` — `Use()` calls after that point throw `InvalidOperationException`.
- `RequestHandler.Middleware` exposes registration metadata only (`MiddlewareDescriptor`: the middleware type for `Use<T>()`; null type for delegates, with `DisplayName` = method name for method groups or `MiddlewareDescriptor.DelegateDisplayName` for lambdas) — the component delegates and the compiled pipeline stay private. List order is registration order, which is inbound execution order.
- `TRequest` and `TResponse` are both `where … : notnull` (not `class`) so value-type requests/responses work. `RequestContext.Response` is non-nullable (`TResponse`, initialized to `default!`) and `InvokeAsync` returns `Task<TResponse>` — middleware is expected to assign `Response` before the pipeline returns; for event-style pipelines `TResponse` is `Unit`, whose `default` is its sole value.
- Cross-middleware state goes through `RequestContext.Data`. `Unit` is the response type for event-style pipelines.
- `RequestContext` is single-threaded per request — the pipeline invokes middleware sequentially, and the context (including `Data` and `Response`) is deliberately unsynchronized, matching `HttpContext` semantics. Middleware that fans out parallel work must not touch the context concurrently. Don't "fix" the lazy `data ??= []` init.
- Class middleware: `RequestMiddleware<TReq, TRes> next` must be the first ctor parameter; `RequestContext` must be the first `InvokeAsync` parameter (additional parameters resolve from the scoped service provider).
- `RequestHandler.Services` (the root provider) is `internal`, surfaced publicly only through `PlumberApplicationFactory.Services` via `InternalsVisibleTo("Plumber.Testing")` — the two assemblies version together. Production code never sees the root provider; middleware use the per-request `RequestContext.Services`.
- `Plumber.Testing` coverage is owned by `Plumber.Testing.Tests` at the solution ratchet; `Sample.Cli.Tests` covers `Sample.Cli` only, under its lowered demo threshold.
