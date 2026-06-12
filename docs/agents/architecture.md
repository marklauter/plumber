---
title: Architecture — non-obvious invariants
summary: Design invariants of the Plumber pipeline that are not derivable from the code at a glance.
tags: [agent-guidance, architecture]
created: 2026-05-10
---

# Architecture — non-obvious invariants

- `RequestHandlerBuilder<TReq, TRes>` is callback-based: `ConfigureServices`/`ConfigureLogging`/`ConfigureConfiguration` queue actions that run during `Build()`. It does not implement `ILoggingBuilder`/`IMetricsBuilder` and exposes no `Services` or `Configuration` properties.
- `IConfiguration` is registered via factory during `Build()`, so the DI container owns its lifetime. Handler `Dispose` only disposes the service provider; configuration is disposed transitively.
- The pipeline is built lazily on first `InvokeAsync()` — `Use()` calls after that point throw `InvalidOperationException`.
- `RequestHandler.Middleware` exposes registration metadata only (`MiddlewareDescriptor`: the middleware type for `Use<T>()`; null type for delegates, with `DisplayName` = method name for method groups or `MiddlewareDescriptor.DelegateDisplayName` for lambdas) — never the component delegates or the compiled pipeline. List order is registration order, which is inbound execution order.
- `TRequest` is `where TRequest : notnull` (not `class`) so value-type requests work.
- Cross-middleware state goes through `RequestContext.Data`. `Unit` is the response type for event-style pipelines.
- Class middleware: `RequestMiddleware<TReq, TRes> next` must be the first ctor parameter; `RequestContext` must be the first `InvokeAsync` parameter (additional parameters resolve from the scoped service provider).
