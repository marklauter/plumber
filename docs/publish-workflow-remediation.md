# Publish Workflow Remediation Plan

Findings from a best-practices review of `.github/workflows/dotnet.publish.yml`.
Trusted Publishing (OIDC) is **deferred** — tracked separately as a cross-repo
initiative — and is intentionally out of scope here.

Ordered by priority. Items are independent unless a dependency is noted.

---

## 1. Gate on the shipped configuration (Release), not Debug

**Important.**

**Problem.** The `test` job builds `-c Debug` and runs `dotnet test --no-build`
(defaults to Debug), but `publish` packs `-c Release`. The Release binaries that
are actually distributed are never compiled or tested at publish time. The
matrix coverage in `dotnet.tests.yml` does not help — that workflow does not run
on the `release` trigger.

**Fix.** Build and test Release in the gate job:

```yaml
    - name: Build
      run: dotnet build -c Release --no-restore
    - name: Test
      run: dotnet test -c Release --no-build
```

**Files.** `.github/workflows/dotnet.publish.yml`.

**Verify.** Trigger a pre-release (or `workflow_dispatch` on a branch) and
confirm the gate job logs `Release` for both build and test.

---

## 2. Derive the package set from project metadata, not the YAML

**Important.** Depends on a one-line project change (2a) before the workflow
change (2b).

**Problem.** Four hand-maintained `dotnet pack src/X` steps duplicate the list
of publishable projects. Adding a fifth library and forgetting a pack step
silently drops it from the release.

**Fix.**

- **2a.** Mark the sample as non-packable so a solution-wide pack only emits the
  four libraries:

  ```xml
  <!-- samples/Sample.Cli/Sample.Cli.csproj -->
  <IsPackable>false</IsPackable>
  ```

  (`*.Tests` projects already set `IsPackable=false` via `Directory.Build.props`.)

- **2b.** Replace the four pack steps with one:

  ```yaml
    - name: Pack
      run: dotnet pack Plumber.slnx -c Release --no-restore -o nuget -p:PackageVersion=$PACKAGE_VERSION -p:Version=$PACKAGE_VERSION
  ```

**Files.** `samples/Sample.Cli/Sample.Cli.csproj`,
`.github/workflows/dotnet.publish.yml`.

**Verify.** Run `dotnet pack Plumber.slnx -c Release -o /tmp/nuget` locally;
confirm exactly four `MSL.Plumber.*.nupkg` (plus matching `.snupkg`) appear and
no `Sample.Cli` package is produced.

---

## 3. Validate the resolved version before packing

**Worth doing.**

**Problem.** `version="${TAG#v}"` strips a leading `v` and trusts the rest. A
malformed or empty tag flows into pack/push and produces a garbage version.

**Fix.** Fail fast after resolving the version:

```yaml
    - name: Resolve package version
      env:
        TAG: ${{ github.event.release.tag_name }}
      run: |
        version="${TAG#v}"
        [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] \
          || { echo "::error::invalid version '$version' from tag '$TAG'"; exit 1; }
        echo "Publishing version $version"
        echo "PACKAGE_VERSION=$version" >> "$GITHUB_ENV"
```

(GitHub's default `bash` shell already runs with `-eo pipefail`, so no extra
`set` is needed.)

**Files.** `.github/workflows/dotnet.publish.yml`.

**Verify.** A tag like `release-foo` or `v1.2` should fail the job at this step.

---

## 4. Add a concurrency guard to the publish workflow

**Minor.** `--skip-duplicate` already makes re-runs idempotent, so this is
defense-in-depth.

**Problem.** Overlapping release runs can race on pack/push.

**Fix.** Add at the workflow top level (mirrors `dotnet.tests.yml`, but without
`cancel-in-progress` — a publish in flight should finish):

```yaml
concurrency:
  group: publish-${{ github.event.release.tag_name }}
  cancel-in-progress: false
```

**Files.** `.github/workflows/dotnet.publish.yml`.

---

## 5. Pin the SDK with global.json

**Minor.** Affects both workflows.

**Problem.** Both workflows float on `dotnet-version: 10.0.x`, so the SDK can
drift between the test gate and any local repro.

**Fix.** Add a `global.json` at the repo root with a `rollForward` policy, e.g.:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Keep `setup-dotnet` as-is; it honors `global.json` when present. Pick the exact
baseline version from the current CI SDK.

**Files.** `global.json` (new). No workflow change required.

**Verify.** `dotnet --version` in CI matches the pinned baseline (modulo
`rollForward`).

---

## 6. De-duplicate job setup (optional, cosmetic)

**Minor.** Lowest priority; purely maintainability.

**Problem.** `checkout` + `setup-dotnet` + NuGet cache are copy-pasted across the
`test` and `publish` jobs (and across both workflows).

**Fix.** Extract a local composite action
(`.github/actions/setup-dotnet/action.yml`) bundling those three steps and
reference it from each job. Defer unless the duplication starts causing drift.

**Files.** `.github/actions/setup-dotnet/action.yml` (new), both workflows.

---

## Suggested sequencing

1. **#1 and #2** together — they are the substantive correctness/safety fixes
   and both touch the publish workflow's core steps.
2. **#3** — small, self-contained hardening.
3. **#4 and #5** — low-risk guards; bundle into the same PR.
4. **#6** — only if/when the duplication becomes a maintenance burden.

All of the above can land in a single PR against `lauterm/otel` (or a dedicated
`ci/publish-hardening` branch) since they are confined to CI config plus one
`IsPackable` flag.
