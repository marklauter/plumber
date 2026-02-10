# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Middleware pipeline framework for C# .NET 8 host-free applications (AWS Lambda, console apps, message queue processors). Implements request-response pattern with middleware, DI, and configuration management.

NuGet package: `MSL.Plumber.Pipeline`

## Quick Reference

- Solution file: `Plumber.sln`
- Build: `dotnet build`
- Test: `dotnet test`
- Single test: `dotnet test --filter "FullyQualifiedName~TestName"`
- Pack: `dotnet pack Plumber/Plumber.csproj -c Release`

## Code Style

Read `.claude/code-style.md` before writing any code. The `.editorconfig` is enforced — fix any violations before committing.

## Testing

Read `.claude/testing.md` before creating test projects and before writing tests.

## Architecture

The pipeline follows a builder → handler → middleware chain pattern:

- `RequestHandlerBuilder` — static factory, creates `IRequestHandlerBuilder<TReq, TRes>` via `Create<TReq, TRes>()`. Configures DI services and configuration sources.
- `IRequestHandlerBuilder<TReq, TRes>` — exposes `Services` (IServiceCollection), `Configuration` (IConfigurationManager), and `Build()`.
- `IRequestHandler<TReq, TRes>` — the built pipeline. Middleware added via fluent `.Use()` calls. Pipeline lazily constructed on first `InvokeAsync()`.
- `RequestContext<TReq, TRes>` — record passed through middleware. Contains `Request`, `Response`, `Id` (ULID), `Timestamp`, `Services` (scoped provider), `CancellationToken`, `Data` dictionary, and `Elapsed`.
- `RequestMiddleware<TReq, TRes>` — delegate type for middleware functions.
- `Void` — readonly record struct for pipelines with no response (event handlers).
- `RequestHandler<TReq, TRes>` (internal) — stores middleware as list of functions, builds pipeline lazily, creates scoped service provider per request, handles timeouts.

### Middleware execution

Middleware forms an onion: code before `await next(context)` runs in registration order, code after runs in reverse. Short-circuit by not calling `next()` and setting `context.Response`. Middleware can be delegate-based (inline lambda) or class-based (must have `InvokeAsync` method with `RequestContext` as first parameter, and `RequestMiddleware<TReq, TRes> next` as first constructor parameter).

### Type constraint

`TRequest` uses `where TRequest : notnull` (not `where class`) to support value types.

### Solution structure

- **Plumber/** — Core library (all pipeline types)
- **Plumber.Tests/** — xUnit tests for the core library
- **Sample.AWSLambda.SQS/** — Example: SQS event handler
- **Sample.AWSLambda.APIGateway/** — Example: API Gateway request handler
- **Sample.AWSLambda.*.Tests/** — Tests for sample projects

## Tech Stack

Read `.claude/tech-stack.md` for platform and dependency info.

## CI/CD

- `.github/workflows/dotnet.tests.yml` — Runs on push/PR to main: restore → build (Debug) → test
- `.github/workflows/dotnet.publish.yml` — On release: test → version from tag → build (Release) → pack → publish to GitHub Packages and NuGet.org
