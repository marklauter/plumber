# Tech debt triage — 2026-05-09

Snapshot after the upgrade-net10 / interface-removal / callback-builder refactor.

**Totals:** 19 entries — 17 Resolved, 2 Open. (No `018-*.md`; that number was retired and partially carved into #020.)

## Open

### #016 — Plumber.Serilog.Extensions port (High)
External package targets the old `builder.Services` / `builder.Configuration` shape and will not compile against the new callback API. Also affected: `IRequestHandler` was deleted, so the `UseSerilogRequestLogging<TReq, TRes>` extension must retarget the concrete `RequestHandler<TReq, TRes>`.

- **Blocking** for any consumer pairing Serilog with this branch's Plumber.
- *Effort:* small port (callbacks instead of direct `Services` / `Configuration`) + tests + version bump + readme/instructions update.
- *Dependencies:* publish a Plumber pre-release, OR develop in lockstep against a project reference (local clone at `.tmp/plumber.serilog.extensions/`).

### #020 — Finite-timeout firing path is untested (Low)
Timeout-fires path on both finite-timeout overloads has no test. The "drop-in `[Fact]` with a 50 ms timeout and 10 s delay" idea doesn't work: the timeout CTS at lines 169 and 182 uses the wall-clock `new CancellationTokenSource(timeout)` and ignores the handler's `TimeProvider`, so `FakeTimeProvider` can't drive it deterministically. Earlier snippet also asserted `OperationCanceledException`, which is wrong post-#008 (now `TimeoutException`).

- *Effort:* ~half-day. Production change: switch to the `(TimeSpan, TimeProvider)` CTS overload in both methods. Test change: pair of `FakeTimeProvider` + TCS rendezvous tests asserting `TimeoutException` with OCE inner.
- *Dependencies:* none.
- *Surface impact:* user-supplied `TimeProvider` now controls timeout firing as well as `RequestContext.Elapsed` — consistent with #012's intent; warrants a one-line note on the `Timeout` property remarks.

## Resolved this branch

Closed by the upgrade-net10 / interface-removal / callback-builder PR plus follow-on test and doc passes:

- **#001** — `EnsureHandler` race → `Lazy<RequestMiddleware<...>>` constructed in ctor.
- **#002** — `ConfigurationManager` never disposed → DI factory registration captures the root for disposal.
- **#003** — Reflection `Invoke` null result → `?? throw new InvalidOperationException(...)` naming the offending method. Regression test added.
- **#004** — `AddUserSecrets` wrong-assembly → user secrets dropped from defaults; opt-in via `AddUserSecrets<T>()`.
- **#005** — `TryGetValue<T>` broken for null / value types → rewritten with `value is T typed`. Regression tests added.
- **#006** — `disposed` field race → not a bug; standard MS pattern. See `feedback_dispose_pattern.md`.
- **#007** — Class middleware singleton lifetime → resolved as documented design (mirrors ASP.NET Core); `<remarks>` on both `Use<TMiddleware>` overloads spell out lifetime + InvokeAsync-parameter escape hatch.
- **#008** — Timeout vs caller cancellation indistinguishable → exception filter translates timeout-induced OCE into `TimeoutException`. Four regression tests including caller-cancel race.
- **#009** — Critical-path coverage → 41 tests, 99%+ line coverage on `Plumber`, includes deterministic two-task TCS rendezvous for overlapping invocations.
- **#010** — `ServiceProvider` on public interface → interface deleted; concrete `RequestHandler.Services` is `IServiceProvider`-typed.
- **#011** — `RequestContext` mutable record → resolved.
- **#012** — `Elapsed` resolution → `RequestContext` takes `TimeProvider`; uses `GetTimestamp()` for monotonic elapsed, `GetUtcNow().UtcDateTime` for wall-clock `Timestamp`. `TryAddSingleton` lets `FakeTimeProvider` win.
- **#013** — Per-invocation `object[]` + `MethodInfo.Invoke` → resolved.
- **#014** — User secrets loaded unconditionally → no longer loaded by defaults.
- **#015** — Multiple `Build()` calls → reframed as intended "recipe pattern"; per-Build snapshot avoids accumulation.
- **#017** — XML doc sweep → all six categories applied; build clean; the dead IDISP007 suppression on `Dispose` removed.
- **#019** — `Build()` leaked configuration on callback failure → try/catch disposes config on failure. Regression tests added.

## Suggested execution order

1. **#020** — drop in the snippet, ~15 min, no design surface.
2. **#016** — port Plumber.Serilog.Extensions once a Plumber pre-release is published, or develop in lockstep via project reference.

## What changed since the last triage entry

- Five items closed: #007 (documented as designed), #011, #012 (TimeProvider migration), #013, #017 (xmldoc sweep landed).
- #020 added (carved out of the now-deleted #018).
- Only one open item with code impact remains, and it is external (#016).
