# XML doc sweep: stale, lying, and missing public-API docs

- **Area:** Public API documentation (Plumber + Plumber.Testing)
- **Priority:** Medium
- **Status:** Resolved — see "Remaining" below

## Problem

After the upgrade-net10 refactors (interface removal, callback-based builder, configuration ownership rework), several public XML doc comments fall into one of these categories:

- **Lying** — describe behavior that no longer matches the code.
- **Wrong** — factual errors (incorrect return types, typos in identifiers).
- **Stale** — reference removed types or transferred ownership patterns that no longer apply.
- **Missing** — public members with no `<param>` or `<returns>` documentation.

Consumers reading these docs from the IDE or generated reference will get inaccurate guidance. The static factory's `Create<>` summary is the worst offender — it actively claims behavior that doesn't happen.

## Remaining

Items 2 (`TryGetValue` doc) and parts of item 6 ("Contructor", "dicionary") were already fixed by an unrelated refactor before this sweep was applied. Items 1, 3, 4, 5, 6 ("cancelation"), and the Optional polish items have been applied and the build is clean (the IDISP007 suppression on `Dispose` was removed — analyzer no longer fires, confirming it was dead). `CLAUDE.md`'s two invariants describing the old builder shape and configuration ownership were also rewritten to match the current callback-based builder and DI-owned configuration.

## Suggested Fix

Work through the items below in priority order. After items 1–4 the docs will match the code; 5–6 is polish.

### 1. Lying — `Plumber/RequestHandlerBuilder.cs` static factory

Both `Create<>` overloads' summaries describe defaults as if they're auto-applied. They aren't — defaults are opt-in via `AddDefaultConfigurationSources()` on the returned builder.

- `Plumber/RequestHandlerBuilder.cs:11` — `Create<TRequest, TResponse>()` summary lists `AddJsonFile`, `AddEnvironmentVariables`, user secrets, and `AddCommandLine` as automatic. Today the builder starts with only `SetBasePath(cwd)`. Rewrite to describe the empty-by-default shape and direct that consumers either chain `AddDefaultConfigurationSources()` or use direct `Add*` methods.
- `Plumber/RequestHandlerBuilder.cs:28` — `Create<TRequest, TResponse>(string[] args)` has the same fictitious default list. Rewrite. Mention that `args` is appended via `AddCommandLine` last in `Build()` so CLI args take precedence.

### 2. Wrong — `Plumber/RequestContext{TRequest, TResponse}.cs:TryGetValue<T>`

- `Plumber/RequestContext{TRequest, TResponse}.cs:37` — `<returns>TData</returns>` is wrong. Method returns `bool`. Replace with: `<returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>`
- `Plumber/RequestContext{TRequest, TResponse}.cs:36` — empty `<param name="item">`. Describe: "Receives the value associated with `key` if found."
- `Plumber/RequestContext{TRequest, TResponse}.cs:38` — typo "dicionary" → "dictionary".

Note: tech debt #005 describes broken behavior in this method for null values and value types. When that fix lands, the doc should also reflect the corrected semantics.

### 3. Stale — `Plumber/RequestHandler{TRequest, TResponse}.cs:Dispose` suppression

- `Plumber/RequestHandler{TRequest, TResponse}.cs:244` — `IDISP007` suppression justification: "ownership of ConfigurationManager transfers from RequestHandlerBuilder at Build() time". This transfer no longer happens; the configuration is DI-managed via factory registration since the recent refactor. Action: remove the suppression entirely; if IDISP007 fires after removal, update the justification to reflect the actual current reason. Otherwise leave it removed.

### 4. Stale — `Plumber/RequestHandler{TRequest, TResponse}.cs:InvokeAsync` remarks

- `Plumber/RequestHandler{TRequest, TResponse}.cs:43-47` and `60-64` — remarks reference "the request handler's ServiceProvider". `RequestHandler` no longer exposes a public `Services` property. Reword to: "Each invocation creates a new DI scope; `RequestContext.Services` is the per-request scoped provider and is disposed when the pipeline returns."

### 5. Missing `<param>` / `<returns>` — sweep

`Plumber/RequestHandler{TRequest, TResponse}.cs`:
- Lines 41, 57, 58 — empty `<param>` blocks on both `InvokeAsync` overloads.
- Lines 42, 59 — `<returns>Task{TResponse}</returns>` is plain text; should describe semantics (returns `null` if no middleware sets `Response`).
- Line 74 — `Use(Func<RequestMiddleware, RequestMiddleware>)` — `<param>` value is type names, not a description. Suggested: "A delegate that receives the next middleware in the chain and returns a wrapped middleware."
- Line 91 — `Use(Func<RequestContext, RequestMiddleware, Task>)` — same: "An async delegate receiving the request context and the next middleware delegate."

`Plumber/RequestMiddleware{TRequest, TResponse}.cs`:
- Line 8 — empty `<param name="context">`. Describe: "The request context flowing through the pipeline."
- Line 9 — empty `<returns>`. Describe: "A `Task` that completes when this middleware (and downstream middleware it calls) finishes."

`Plumber.Testing/PlumberApplicationFactory.cs` — most public methods have summaries but are missing `<param>` / `<returns>`. Add for consistency:
- Line 49 — `WithBuilder`
- Line 69 — `WithServices(Action<IServiceCollection>)`
- Line 78 — `WithServices(Action<IServiceCollection, IConfiguration>)` (also missing `<remarks>` that the simpler overload has)
- Line 87 — `WithLogging`
- Line 96 — `WithConfiguration`
- Line 106 — `WithInMemorySettings` (has `<param>`, missing `<returns>`)
- Line 118 — `CreateHandler` (missing `<returns>`)
- Line 140 — `InvokeAsync` (missing `<param>` and `<returns>`)

### 6. Polish — typos

- `Plumber/RequestHandler{TRequest, TResponse}.cs:34` — "cancelation token" → "cancellation token".
- `Plumber/RequestHandler{TRequest, TResponse}.cs:101` — "Contructor arguments" → "Constructor arguments".
- `Plumber/RequestContext{TRequest, TResponse}.cs:38` — "dicionary" → "dictionary" (also covered in item 2).

### Optional

- `Plumber/RequestHandler{TRequest, TResponse}.cs:7` — class summary could mention "Obtained from `RequestHandlerBuilder.Build()`; not constructable directly" since the ctor is internal.
- `Plumber/RequestHandlerBuilder.cs:5` — class summary "Extensions to create new typed request handler builders" reads like extension methods. Reword to: "Static factory for `RequestHandlerBuilder<TRequest, TResponse>`."

## Code References

- `Plumber/RequestHandlerBuilder.cs:11,28` — lying default-source claims in both `Create<>` overloads
- `Plumber/RequestContext{TRequest, TResponse}.cs:31-40` — `TryGetValue<T>` wrong return doc + typo
- `Plumber/RequestHandler{TRequest, TResponse}.cs:43-47,60-64` — stale `Services` references in `InvokeAsync` remarks
- `Plumber/RequestHandler{TRequest, TResponse}.cs:244` — stale IDISP007 justification on `Dispose`
- `Plumber/RequestHandler{TRequest, TResponse}.cs:41,57,58,74,91` — missing/uninformative `<param>`/`<returns>` blocks
- `Plumber/RequestMiddleware{TRequest, TResponse}.cs:8-9` — empty `<param>` and `<returns>`
- `Plumber.Testing/PlumberApplicationFactory.cs:49,69,78,87,96,106,118,140` — missing `<param>`/`<returns>` across most public methods

## Notes

- `RequestHandlerBuilder<TRequest, TResponse>` itself was cleaned up during the recent work; all its public members already have parallel `<summary>` + `<param>` + `<returns>` docs and don't appear in this list.
- `Plumber/Unit.cs` was reviewed and is clean.
- README.md and `instructions-for-llms*.md` are being rewritten in a separate session; their drift is tracked there, not here.
