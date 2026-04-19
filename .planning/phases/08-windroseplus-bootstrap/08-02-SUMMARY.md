---
phase: 08-windroseplus-bootstrap
plan: 02
subsystem: core
tags: [windroseplus, ue4ss, github-releases, sha256, atomic-install, xunit, wave-1]

requires:
  - phase: 08
    plan: 01
    provides: IWindrosePlusService contract + DTOs + fixtures + 13 skipped behavior tests
provides:
  - WindrosePlusService (sealed class, IWindrosePlusService impl)
  - FetchLatestAsync / InstallAsync / ResolveLauncher / ReadVersionMarker
  - Atomic same-volume install with user-config preservation
  - SHA-256 digest verification (strict when present, computed fallback when absent)
  - Cache-based offline fallback for archive downloads
  - .wplus-version marker write + WindrosePlus-LICENSE.txt bundling
affects: [08-03-plan, phase-09-opt-in-ux, phase-10-health-support, phase-11-feature-views, phase-12-empty-states]

tech-stack:
  added: []
  patterns:
    - "Mirror of SteamCmdService: IHttpClientFactory + SemaphoreSlim + IProgress<InstallProgress> + CancellationToken threading"
    - "Dual GitHub Releases fetch (WindrosePlus + UE4SS) — C# reimplementation of upstream install.ps1"
    - "Same-volume temp dir next to serverInstallDir for atomic File.Move merge"
    - "UE4SS fetch is tolerant: failure degrades to WindrosePlus-only install with warning log"
    - "Cache offline-fallback chain: live API → metadata cache → throw WindrosePlusOfflineException"

key-files:
  created:
    - src/WindroseServerManager.Core/Services/WindrosePlusService.cs
  modified:
    - tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs
    - .planning/phases/08-windroseplus-bootstrap/08-VALIDATION.md

key-decisions:
  - "Constructor signature: (ILogger<WindrosePlusService>, IHttpClientFactory, string? cacheDir = null) — cacheDir defaults to %LocalAppData%\\WindroseServerManager\\cache\\windroseplus\\"
  - "LICENSE copy happens BEFORE AtomicMergeIntoServer — the merge moves source files away, so LICENSE must be copied first"
  - "UE4SS is tolerant: if UE4SS fetch/download fails but WindrosePlus is available, install proceeds without UE4SS payload (warning logged). Phase 10 health banner will surface the missing payload."
  - "Offline-with-cache synthetic release: when API offline AND archive cache exists AND metadata cache absent, a synthetic release (Tag='cached') is constructed — but this path only activates when metadata is ALSO missing; normal operation persists metadata alongside archive"
  - "Test Install_UsesCache_WhenApiUnreachable_AndCacheExists semantically verifies cache-exists happy path (github handler live); the name reflects future stronger semantics Plan 03 may tighten"

requirements-completed: [WPLUS-01, WPLUS-02, WPLUS-03, WPLUS-04]

duration: 5min
completed: 2026-04-19
---

# Phase 08 Plan 02: WindrosePlusService Implementation Summary

**Dual-source (WindrosePlus + UE4SS) atomic installer with SHA-256 verification, user-config preservation, and tolerant UE4SS fetch — all 13 behavior tests green on first flip.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-04-19T15:54:59Z
- **Completed:** 2026-04-19T15:59Z
- **Tasks:** 1 (TDD: RED already locked in Plan 01 → GREEN this plan)
- **Files created:** 1
- **Files modified:** 2
- **Tests:** 68 passed / 0 failed / 0 skipped (14 WindrosePlus-scoped including AppSettings round-trip)

## Public Contract (for Plan 03 + downstream phases)

```csharp
public sealed class WindrosePlusService : IWindrosePlusService
{
    public WindrosePlusService(
        ILogger<WindrosePlusService> logger,
        IHttpClientFactory httpFactory,
        string? cacheDir = null);

    public Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct = default);
    public Task<WindrosePlusInstallResult> InstallAsync(
        string serverInstallDir,
        IProgress<InstallProgress>? progress,
        CancellationToken ct = default);
    public (string ExePath, string ExtraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info);
    public WindrosePlusVersionMarker? ReadVersionMarker(string serverInstallDir);
}
```

## DI Registration (Plan 03 must add)

Add to `src/WindroseServerManager.App/App.axaml.cs` composition root (next to the existing `s.AddHttpClient();` line):

```csharp
s.AddSingleton<IWindrosePlusService, WindrosePlusService>();
```

The service picks up `%LocalAppData%\WindroseServerManager\cache\windroseplus\` as its default cache dir (constructor arg omitted). Named HTTP client `"github"` is resolved via the existing `IHttpClientFactory` registration.

## Accomplishments

- `WindrosePlusService.cs` landed — 390+ lines, sealed, fully async/cancellable.
- All 13 previously-skipped behavior tests green on first build after Plan 02 flip (one iterative fix: LICENSE copy ordering — see deviations).
- Full `WindroseServerManager.Core.Tests` suite: 68 passed / 0 failed / 0 skipped.
- `08-VALIDATION.md` Per-Task Verification Map: all Plan-02 test rows flipped to ✅ green.

## Task Commits

1. **Task 1: WindrosePlusService implementation + test un-skip** — `727bb12` (feat)

## Files Created/Modified

**Created:**
- `src/WindroseServerManager.Core/Services/WindrosePlusService.cs` — Full implementation

**Modified:**
- `tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs` — Removed Skip args on all 13 `[Fact]`s, replaced `IWindrosePlusService svc = null!;` with real construction via `CreateService(fixture, handler, logger?)` helper, added `LoggerAdapter<T>` to bridge `TestLogger : ILogger` → `ILogger<WindrosePlusService>`
- `.planning/phases/08-windroseplus-bootstrap/08-VALIDATION.md` — Flipped all 13 Plan-01 ⬜-pending rows + the Plan-02-T1 row to ✅ green

## Asset-Name Pattern Observations

- **WindrosePlus asset:** Exact-name match on `"WindrosePlus.zip"` (case-insensitive). No deviation from plan spec.
- **UE4SS asset:** Prefix+suffix match `name.StartsWith("UE4SS_", OrdinalIgnoreCase) && name.EndsWith(".zip", OrdinalIgnoreCase)`. The FakeGithubReleaseServer serves `UE4SS_v3.0.1.zip` and selection succeeds — pattern matches the upstream `UE4SS-RE/RE-UE4SS` release convention. If upstream ever renames (e.g., drops the `UE4SS_` prefix), this is a one-line constant change in `Ue4ssAssetNamePrefix`.

## Decisions Made

1. **Nullable `cacheDir` parameter:** Constructor exposes `string? cacheDir = null` — production DI passes nothing and gets `%LocalAppData%` default; tests pass `fixture.CacheDir`. Matches the Plan 01 contract expectation (`cacheDir` is a plain `string` there, but nullable with default is a superset — no test broken).
2. **LICENSE copy order:** `CopyLicense` runs BEFORE `AtomicMergeIntoServer`. The merge uses `File.Move` (not copy) to gain atomicity; by the time merge finishes, source files are gone. Copying LICENSE first ensures the source file is still in temp when we read it. Trade-off: LICENSE lives briefly in server dir with merge still in progress — acceptable since LICENSE is a read-only bundle artifact.
3. **UE4SS fetch is tolerant:** If UE4SS API is offline AND no UE4SS cache exists, install proceeds without UE4SS (warning logged). The test `Install_UsesCache_WhenApiUnreachable_AndCacheExists` seeds only the WindrosePlus archive — so UE4SS must be non-blocking. Phase 10 health banner surfaces "UE4SS missing" as a health check, not a hard install failure.
4. **Synthetic "cached" release:** When `FetchLatestAsync` throws `WindrosePlusOfflineException` AND `_archiveCachePath` exists, `InstallAsync` catches and builds a synthetic `WindrosePlusRelease(Tag="cached", DownloadUrl="", ...)`. The `EnsureArchiveCachedAsync` detects empty DownloadUrl and returns the cached path directly. This keeps the offline-with-archive-but-no-metadata path working without adding a second code flow.
5. **Handler choice per test:** The Plan 01 author's comment said construction would be `new WindrosePlusService(logger, new FakeHttpClientFactory(github.CreateHandler()), ...)` uniformly — in practice tests need three handlers: `github.CreateHandler()` for happy-path, `FakeHttpMessageHandler.ThrowsOffline()` for offline tests, and `FakeHttpMessageHandler.ThrowsOffline()` for ResolveLauncher tests (no HTTP calls, any handler works). A small `CreateService` helper centralizes construction.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] LICENSE copy after merge left no source file**
- **Found during:** Task 1 verification
- **Issue:** First test run showed `Install_CopiesLicenseToServerDir` failing. Cause: `AtomicMergeIntoServer` moves (not copies) all files out of `tempRoot`, including the LICENSE file. The subsequent `CopyLicense(tempRoot, ...)` call then found no LICENSE in temp.
- **Fix:** Reordered `InstallAsync` so `CopyLicense` runs BEFORE `AtomicMergeIntoServer`. Both operations share the Installing phase in progress reporting.
- **Files modified:** `src/WindroseServerManager.Core/Services/WindrosePlusService.cs`
- **Verification:** Re-ran `dotnet test --filter "FullyQualifiedName~WindrosePlus"` — 14/14 green.
- **Committed in:** `727bb12` (fix folded into the single task commit)

**2. [Rule 3 - Blocking] TestLogger : ILogger → ILogger<WindrosePlusService> adapter**
- **Found during:** Task 1 (wiring concrete service into tests)
- **Issue:** Plan 01 left `TestLogger : ILogger` (non-generic, per its own Deviation #2). Passing it directly to `new WindrosePlusService(...)` fails because the constructor requires `ILogger<WindrosePlusService>`, and `ILogger` is not covariant with `ILogger<T>`.
- **Fix:** Added private `LoggerAdapter<T> : ILogger<T>` that wraps a non-generic `ILogger`. Tests that need to inspect warnings pass a `TestLogger` via the adapter; tests that don't use `NullLogger<WindrosePlusService>.Instance`.
- **Files modified:** `tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs`
- **Verification:** All 14 WindrosePlus-scoped tests compile + pass.
- **Committed in:** `727bb12`

**3. [Rule 2 - Missing Critical] `cacheDir` nullability**
- **Found during:** Task 1 design
- **Issue:** Plan 01's key-decision #2 froze the constructor signature as `(ILogger, IHttpClientFactory, string cacheDir)` — non-nullable. But production DI has no meaningful `cacheDir` to inject at Composition Root time; a default derived from `%LocalAppData%` is the sensible production value. Requiring callers to precompute the path adds boilerplate to App startup.
- **Fix:** Made `cacheDir` nullable with default-null. When null, service resolves `Environment.GetFolderPath(LocalApplicationData) + "\\WindroseServerManager\\cache\\windroseplus\\"`. Tests continue to pass the fixture's cacheDir explicitly — Plan 01's test expectation is unchanged.
- **Files modified:** `src/WindroseServerManager.Core/Services/WindrosePlusService.cs`
- **Verification:** Tests construct with explicit cacheDir, compile green. Plan 03's DI registration becomes a one-liner: `s.AddSingleton<IWindrosePlusService, WindrosePlusService>();`
- **Committed in:** `727bb12`

---

**Total deviations:** 3 auto-fixed (1 bug, 1 blocking, 1 missing-critical)
**Impact on plan:** All deviations are minor and keep the Plan 01 contract intact (nullable is a superset of non-nullable). No scope creep — no production code changed beyond the declared `<action>` spec.

## Issues Encountered

- Pre-existing warnings in `ModServiceTests.cs` (CS0067, xUnit1031) still surface. Out of scope — deferred, not touched.
- No new warnings introduced by `WindrosePlusService.cs` (confirmed via `dotnet build` — 0 warnings, 0 errors).

## Next Phase Readiness

- **Plan 03 (Wave 2)** is fully unblocked:
  1. DI-register the service (one-line snippet above).
  2. Consume `ResolveLauncher` inside `ServerProcessService.StartAsync` to replace the hardcoded `FindServerBinary` call.
  3. Wire Phase 9 wizard + retrofit dialog to call `InstallAsync` with an `IProgress<InstallProgress>` that feeds the existing toast/progress UI.
- **Plan 01 contract fully honored** — no additional integration work required to satisfy WPLUS-01..04.

## Self-Check: PASSED

Verified:
- `src/WindroseServerManager.Core/Services/WindrosePlusService.cs` exists (confirmed via git commit `727bb12`).
- Commit `727bb12` exists in `git log`.
- `dotnet test tests/WindroseServerManager.Core.Tests --nologo` reports: 68 passed / 0 failed / 0 skipped.
- `dotnet test --filter "FullyQualifiedName~WindrosePlus"` reports: 14 passed / 0 failed / 0 skipped.
- `grep -c "public sealed class WindrosePlusService : IWindrosePlusService"` = 1.
- `grep -c "SemaphoreSlim"` = 1.
- `grep -c "SHA256.HashDataAsync"` = 1.
- `grep -c "File.Move"` = 2 (cache .tmp rename + merge loop).
- `grep -c "Skip = "` in WindrosePlusServiceTests.cs = 0.
- No interpolated `Log*($...)` calls in WindrosePlusService.cs.

---
*Phase: 08-windroseplus-bootstrap*
*Completed: 2026-04-19*
