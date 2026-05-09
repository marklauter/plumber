# Tech debt triage — 2026-05-09

Snapshot after the upgrade-net10 / interface-removal / callback-builder refactor.

**Totals:** 19 entries — 12 Resolved, 7 Open.

## Resolved this branch

- **#002** — `ConfigurationManager` never disposed → DI factory registration captures the root for disposal.
- **#004** — `AddUserSecrets` used wrong assembly → user secrets dropped from defaults; consumers opt in via `AddUserSecrets<T>()` from their assembly.
- **#010** — `ServiceProvider` exposed on public interface → interface deleted; concrete `RequestHandler.Services` is `IServiceProvider`-typed.
- **#014** — User secrets loaded unconditionally → no longer loaded by defaults; nothing left to gate.
- **#015** — Builder allowed multiple `Build()` calls → reframed as intended "recipe pattern"; per-Build snapshot avoids source/watcher accumulation.
- **#005** — `TryGetValue<T>` broken for null values and value types → rewritten with `value is T typed` pattern; also fixes a `InvalidCastException` on type mismatch. Regression tests added.
- **#019** — `Build()` leaked the `IConfigurationRoot` if a `ConfigureServices`/`ConfigureLogging` callback (or `BuildServiceProvider()`) threw → `try`/`catch` around the post-Build section disposes the configuration on failure. Regression tests added.
- **#003** — Reflection `Invoke` null result caused undescriptive `NullReferenceException` in `MiddlewareFactory.CreateInjectedMiddleware` → null-forgiving operator replaced with `?? throw new InvalidOperationException(...)` that names the offending method. Regression test added.
- **#001** — `handler ??= BuildPipeline()` race in `EnsureHandler` → `handler` is now `Lazy<RequestMiddleware<...>>` constructed in the ctor with `BuildPipeline` as factory. `EnsureHandler` deleted; `Use` guard uses `IsValueCreated`. Scope clarified: `Use` is single-threaded by contract, only `InvokeAsync` needed thread-safety.
- **#006** — `disposed` field race in `Dispose()` / `ThrowIfDisposed()` → **not a bug.** Bool writes are atomic in .NET, `IDisposable.Dispose` isn't contractually thread-safe, and the underlying `ServiceProvider`/`ConfigurationManager` tolerate double-dispose. Realistic usage disposes once at app shutdown after invokes have stopped — no concurrent path into `Dispose`. The standard `if (disposed) return; disposed = true;` guard is the MS-recommended pattern and is correct here.
- **#008** — Timeout cancellation indistinguishable from user cancellation → both timeout overloads in `RequestHandler.InvokeInternalAsync` now wrap the inner invocation in an exception filter that translates timeout-induced `OperationCanceledException` into `TimeoutException` (with the OCE preserved as `InnerException`). Caller cancellation wins on a race. Four regression tests added covering both overloads, the caller-cancel path, and the race.
- **#009** — Missing test coverage for critical paths → coverage closed across batches A/B/C plus follow-on test passes. Suite now at 41 tests, 99%+ line coverage on `Plumber`. Includes a deterministic two-task TCS rendezvous covering overlapping invocations (proves per-invocation context/scope isolation and the `Lazy<T>` build-once invariant without timing-dependent flake). The stale `Create(args, configure)` bullet is N/A — that overload no longer exists.

## Open — grouped by recommended PR

### Batch C: Middleware factory correctness + diagnostics
- **#007** — Constructor-injected scoped services bind to root provider. **Decide before writing code:** document the limitation OR per-request middleware instantiation. (a) is cheap, (b) costs allocation. The Plumber.Serilog.Extensions package's `RequestLoggerMiddleware` may be affected — worth checking before deciding.

  *Effort:* small-to-medium depending on choice. *Dependencies:* design call. *Tests:* needs new coverage (#009).

### Batch D: External coordination
- **#016** — Plumber.Serilog.Extensions needs to be ported to the new builder shape. **Blocking** for any consumer that uses Serilog with this branch's Plumber. Local clone exists at `.tmp/plumber.serilog.extensions/` for reference.

  *Effort:* small port (callbacks instead of `Services`/`Configuration` direct access) + tests + version bump + readme update.
  *Dependencies:* publish a pre-release of Plumber first OR develop in lockstep against a project reference.

### Batch E: Documentation
- **#017** — XML doc sweep across public API (lying, stale, missing). Six categories listed in the entry. Item 1 (`RequestHandlerBuilder.cs` `Create<>` lying about defaults) is the most urgent — actively misleads consumers.

  *Effort:* medium (mostly mechanical). *Dependencies:* none. *In flight:* a separate session is rewriting README and instructions; this entry covers code XML docs only.

### Defer (cosmetic / breaking-change track)
- **#011** — `RequestContext` is a mutable record. Breaking type change; not urgent.
- **#012** — `Elapsed` uses `DateTime.UtcNow` instead of `Stopwatch`. Resolution is sub-15ms; only matters for perf metrics.
- **#013** — Per-invocation `object[]` allocation + `MethodInfo.Invoke`. Real cost only at high throughput. Compiled-expression / source-generator fix; defer until a perf signal demands it.

## Suggested execution order

1. **Batch A** (#003 + #005, with tests) — half a day, no API impact. *(closed)*
2. **Batch D** (#016) once any of A is published as a Plumber pre-release; otherwise can move in parallel against a project reference.
3. **Batch C** (#007, after deciding the policy) — ~one day. *(#008 closed)*
4. **Batch E** (#017) — can run in parallel with any of the above; mechanical work, no design tension.
5. **Defer** (#011 / #012 / #013) — bundle into next minor/major release.

## What changed since the last triage

- Five items closed by the interface-removal + callback-builder PR.
- Two new items added (#016 Serilog port, #017 xmldoc sweep).
- The original "Concurrency PR" recommendation lost #015 — it became a "recipe pattern" feature rather than a guard.
- The original "Middleware factory PR" recommendation is unchanged in shape but now strictly means #007 (#008 closed; #013 explicitly deferred).
