# CLAUDE.md

## Code style

Read `.claude/code-style.md` before writing any code. The `.editorconfig` is enforced — fix any violations before committing.

## Testing

Read `.claude/testing.md` before creating test projects and before writing tests.

## Tech stack

Read `.claude/tech-stack.md` for platform and dependency info.

## Architecture — non-obvious invariants

- `RequestHandlerBuilder<TReq, TRes>` implements `ILoggingBuilder` + `IMetricsBuilder` in addition to exposing `Services` and `Configuration`.
- `ConfigurationManager` ownership transfers from builder to handler at `Build()`. The handler disposes both the service provider and the configuration manager.
- The pipeline is built lazily on the first `InvokeAsync()` — `Use()` calls after that point will not be picked up.
- `TRequest` is constrained `where TRequest : notnull` (not `class`) so value-type requests work.
- Cross-middleware state goes through `RequestContext.Data`. `Unit` is the response type for event-style pipelines with no return value.
- Class middleware constructor signature: `RequestMiddleware<TReq, TRes> next` must be the first parameter. `InvokeAsync` signature: `RequestContext` must be the first parameter (additional parameters are resolved from the scoped service provider).
