---
title: Plumber src review surfaces eight findings
summary: Code review of src/Plumber and Plumber.Testing found one security-relevant default, two runtime bugs, and five lesser flaws. No injection-style vulnerabilities.
tags: [note, code-review, csharp, plumber]
created: 2026-06-12
document:
  status: open
---

# Plumber src review surfaces eight findings

Code review of `src/Plumber` and `tests/Plumber.Testing` found one security-relevant default, two runtime bugs, and five lesser flaws. No injection-style vulnerabilities. Findings ordered by severity; line refs are as of commit 5d014f5.

## 1. AddDefaultConfigurationSources defaults the environment to Development — fixed

Fixed 2026-06-12: the fallback is now `Production` and the XML doc states the default. Original finding: `RequestHandlerBuilder{TRequest, TResponse}.cs:209` fell back to `Development` when `DOTNET_ENVIRONMENT` was unset, inverting the .NET host convention — a production deployment that forgot the variable silently loaded `appsettings.Development.json`.

## 2. Synchronous DI scope disposal — fixed

Fixed 2026-06-12: the per-request scope is now `await using serviceProvider.CreateAsyncScope()`, and `RequestHandler` and `PlumberApplicationFactory` implement `IAsyncDisposable` so the root provider disposes async-only singletons correctly (major-version change, accepted). Original finding: `using var serviceScope = serviceProvider.CreateScope()` made a scoped service implementing only `IAsyncDisposable` throw `InvalidOperationException` from the scope's `Dispose()` at the end of every request.

## 3. Middleware constructed at first invoke, not registration

`RequestHandler{TRequest, TResponse}.cs:170-206` remarks claim the instance is "constructed once at registration time," but the `MiddlewareFactory` lambda runs inside `BuildPipeline()` on the first `InvokeAsync`. All `Use<TMiddleware>()` validation (missing `InvokeAsync`, wrong first parameter, unresolvable ctor args) fails late at first invocation. Validate the shape eagerly in `Use<TMiddleware>()` and fix the doc.

## 4. Check-then-act race between Use and first invoke

`RequestHandler{TRequest, TResponse}.cs:218-225` checks `handler.IsValueCreated` then mutates `components`/`descriptors`. A `Use` racing the first `InvokeAsync` adds middleware that silently never executes — the `Lazy` already snapshotted the list. Low severity; configuration is normally single-threaded. A `Lock` or list freeze closes it.

## 5. RequestContext.Data lazy init is unsynchronized — fixed

Fixed 2026-06-12: resolved as a documented contract, not a code change. `RequestContext` now carries a `<remarks>` declaring it not thread-safe (pipeline invokes middleware sequentially; parallel branches must not touch the context concurrently), with a matching note on `Data` and an invariant entry in `docs/agents/architecture.md`. `ConcurrentDictionary` was rejected: it would advertise a thread-safety guarantee the rest of the type (`Response` setter) doesn't honor, and `HttpContext.Items` sets the same precedent. Original finding: `RequestContext{TRequest, TResponse}.cs:70` — `data ??= []` from concurrent middleware branches within one request can create two dictionaries and drop writes; `Dictionary` corrupts under concurrent mutation.

## 6. Overloaded InvokeAsync throws AmbiguousMatchException

`RequestHandler{TRequest, TResponse}.cs:302` — `type.GetMethod(name, flags)` throws `AmbiguousMatchException` when the middleware class overloads `InvokeAsync`. Filter `GetMethods()` for the overload whose first parameter is the context type; gives a named error per the fail-loud rule.

## 7. Shared file providers across Build calls

`RequestHandlerBuilder{TRequest, TResponse}.cs:258-272` — the per-build copy reuses source instances. For JSON sources with `reloadOnChange: true`, the first `Build()` caches a `PhysicalFileProvider` on the shared source; subsequent handlers share that watcher and disposing one configuration root leaves it alive. Process-lifetime leak of one watcher per source. Contradicts the "independent handler" doc claim in spirit.

## 8. Smaller items

- `InvokeAsync(TRequest)` at `RequestHandler{TRequest, TResponse}.cs:91` lacks `ArgumentNullException.ThrowIfNull(request)` — the only public entry point without the guard.
- If the `RequestHandler` ctor throws after `BuildServiceProvider()` succeeds (e.g. a user-registered `TimeProvider` factory throws), the built provider leaks; `Build()`'s catch at `RequestHandlerBuilder{TRequest, TResponse}.cs:244-251` disposes only the configuration.
- `PlumberApplicationFactory.CreateHandler` (`PlumberApplicationFactory.cs:133-160`) is check-then-act on `handler` — concurrent callers build two handlers, one never disposed. And if `configurePipeline` returns a different handler than the one passed in, the original `built` instance leaks; the IDISP suppression assumes identity but nothing enforces it.
