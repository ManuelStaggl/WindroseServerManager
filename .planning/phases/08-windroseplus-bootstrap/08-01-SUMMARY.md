---
phase: 08-windroseplus-bootstrap
plan: 01
subsystem: infra
tags: [windroseplus, ue4ss, github-releases, sha256, xunit, contract-first, wpf]

requires:
  - phase: 07
    provides: Nexus-mod/URL-only pattern, existing Core models (InstallProgress, ServerInstallInfo, AppSettings), xUnit test harness
provides:
  - IWindrosePlusService contract (Fetch/Install/ResolveLauncher/ReadVersionMarker)
  - WindrosePlusRelease / WindrosePlusInstallResult / WindrosePlusVersionMarker DTOs
  - WindrosePlusOfflineException and WindrosePlusDigestMismatchException types
  - InstallProgress extended with FetchingRelease/DownloadingArchive/VerifyingDigest/Extracting/Installing/WritingMarker/CleaningUp phases
  - ServerInstallInfo extended with WindrosePlusActive + WindrosePlusVersionTag (back-compat defaults)
  - AppSettings.WindrosePlusActiveByServer and WindrosePlusVersionByServer dictionaries
  - Test fixtures: FakeHttpMessageHandler, FakeHttpClientFactory, SampleArchiveBuilder, FakeGithubReleaseServer, TempServerFixture
  - 13 failing-behavior unit tests locked in as [Fact(Skip=...)] targeting Plan 02
  - AppSettings WindrosePlus round-trip test (green)
affects: [08-02-plan, 08-03-plan, phase-09-opt-in-ux, phase-10-health-support, phase-11-feature-views, phase-12-empty-states]

tech-stack:
  added: []
  patterns:
    - "Contract-first scaffolding: interface + DTOs + failing tests shipped before implementation"
    - "GitHub Releases digest envelope: 'sha256:<hex>' string field, nullable for pre-June-2025 releases"
    - "Per-server WindrosePlus state keyed by normalized InstallDir path in AppSettings dictionaries"
    - "Wave-0 skipped-test harness: concrete class absent, [Fact(Skip=...)] ensures green suite while locking behavior surface"

key-files:
  created:
    - src/WindroseServerManager.Core/Services/IWindrosePlusService.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusRelease.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusInstallResult.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusVersionMarker.cs
    - tests/WindroseServerManager.Core.Tests/TestDoubles/FakeHttpMessageHandler.cs
    - tests/WindroseServerManager.Core.Tests/TestDoubles/FakeHttpClientFactory.cs
    - tests/WindroseServerManager.Core.Tests/Fixtures/SampleArchiveBuilder.cs
    - tests/WindroseServerManager.Core.Tests/Fixtures/FakeGithubReleaseServer.cs
    - tests/WindroseServerManager.Core.Tests/Fixtures/TempServerFixture.cs
    - tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs
  modified:
    - src/WindroseServerManager.Core/Models/InstallProgress.cs
    - src/WindroseServerManager.Core/Models/ServerInstallInfo.cs
    - src/WindroseServerManager.Core/Models/AppSettings.cs
    - tests/WindroseServerManager.Core.Tests/AppSettingsTests.cs
    - .planning/phases/08-windroseplus-bootstrap/08-VALIDATION.md

key-decisions:
  - "License filename inside server dir: WindrosePlus-LICENSE.txt (not LICENSE.WindrosePlus.txt) — chosen from the two options in the plan, prefix groups all WP-bundled artifacts alphabetically near .wplus-version marker"
  - "Expected WindrosePlusService constructor signature for Plan 02: (ILogger<WindrosePlusService> logger, IHttpClientFactory httpClientFactory, string cacheDir) — matches the `NullLogger<WindrosePlusService>.Instance, new FakeHttpClientFactory(...), cacheDir: fixture.CacheDir` expression shown in the plan action"
  - "ServerInstallInfo stays an immutable record with new fields defaulted (WindrosePlusActive=false, WindrosePlusVersionTag=null) — preserves every existing call-site without touching them"
  - "Per-server WP state lives in AppSettings dictionaries keyed by full InstallDir path; ServerInstallInfo fields are populated via read-through in Plan 02, not persisted twice"
  - "Wave-0 test file compiles against IWindrosePlusService only (`IWindrosePlusService svc = null!;`). Plan 02 replaces the null! with `new WindrosePlusService(logger, factory, cacheDir)` AND removes the Skip argument from every [Fact]"
  - "FakeGithubReleaseServer adds a FailWindrosePlusAsset toggle (beyond the plan spec) so Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir can simulate a mid-download 500 without tampering bytes — kept as a public setter, tests still compile green"

patterns-established:
  - "Wave-0 skipped-test contract: all behavior-locking tests use [Fact(Skip = SkipReason)]; Plan 02 removes the Skip argument (not the attribute), minimizing diff"
  - "TestLogger : ILogger — inline non-generic logger captures Warnings/Errors. Concrete service gets ILogger<WindrosePlusService> but TestLogger satisfies the non-generic ILogger contract which ILogger<T> also implements"
  - "FakeGithubReleaseServer scripted-URL pattern: api endpoints return templated JSON, asset endpoints return raw bytes, toggles (PublishDigest, TamperArchive, FailWindrosePlusAsset) cover all failure modes from one fixture"

requirements-completed: [WPLUS-01, WPLUS-02, WPLUS-03, WPLUS-04]

duration: 13min
completed: 2026-04-19
---

# Phase 08 Plan 01: WindrosePlus Bootstrap Scaffolding Summary

**Contract-first IWindrosePlusService surface + failing-behavior xUnit stubs locked in; Plan 02 implements the class against 13 pre-written tests.**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-04-19T15:38Z
- **Completed:** 2026-04-19T15:51Z
- **Tasks:** 3
- **Files created:** 10
- **Files modified:** 5

## Accomplishments

- `IWindrosePlusService` contract finalized — every method signature Plan 02 must match is in place.
- All WindrosePlus DTOs compiled: `WindrosePlusRelease`, `WindrosePlusInstallResult`, `WindrosePlusVersionMarker`.
- Two exception types (`WindrosePlusOfflineException`, `WindrosePlusDigestMismatchException`) available for tests to assert against.
- `InstallProgress`, `ServerInstallInfo`, `AppSettings` extended without breaking any existing call-site or test.
- Test fixtures land: in-process HTTP handler, fake GitHub release server with digest/tamper/asset-failure toggles, in-memory archive builder with authentic WindrosePlus+UE4SS layouts and correct SHA-256s, temp-server fixture with self-cleanup.
- 13 behavior-locking WindrosePlusService tests shipped as `[Fact(Skip=...)]` — Plan 02 flips them to green by writing the implementation.
- AppSettings WindrosePlus round-trip test passes green (validates the new dictionaries survive System.Text.Json serialize/deserialize).
- Full solution builds; `dotnet test` reports 55 passed, 13 skipped, 0 failed.

## Task Commits

1. **Task 1: Extend models + add WindrosePlus DTOs** — `0327a9d` (feat)
2. **Task 2: Test doubles + fixtures** — `9e4f2be` (test)
3. **Task 3: Failing test stubs + AppSettings round-trip** — `149c491` (test)

## Files Created/Modified

**Created:**
- `src/WindroseServerManager.Core/Services/IWindrosePlusService.cs` — Service contract + two exception types
- `src/WindroseServerManager.Core/Models/WindrosePlusRelease.cs` — Parsed GitHub release DTO
- `src/WindroseServerManager.Core/Models/WindrosePlusInstallResult.cs` — Return type for InstallAsync
- `src/WindroseServerManager.Core/Models/WindrosePlusVersionMarker.cs` — JSON-serializable `.wplus-version` marker
- `tests/WindroseServerManager.Core.Tests/TestDoubles/FakeHttpMessageHandler.cs` — Records requests, scripted responses, `ThrowsOffline()` helper
- `tests/WindroseServerManager.Core.Tests/TestDoubles/FakeHttpClientFactory.cs` — Wraps a handler as `IHttpClientFactory`
- `tests/WindroseServerManager.Core.Tests/Fixtures/SampleArchiveBuilder.cs` — Builds WindrosePlus.zip (with `MIT License` LICENSE) and UE4SS.zip in-memory + SHA-256 hex
- `tests/WindroseServerManager.Core.Tests/Fixtures/FakeGithubReleaseServer.cs` — `/releases/latest` JSON + asset bytes for both repos with `PublishDigest`/`TamperArchive`/`FailWindrosePlusAsset` toggles
- `tests/WindroseServerManager.Core.Tests/Fixtures/TempServerFixture.cs` — Disposable temp server+cache dirs with pre-seeded `WindroseServer.exe`
- `tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs` — 13 `[Fact(Skip=...)]` behavior tests + inline TestLogger

**Modified:**
- `src/WindroseServerManager.Core/Models/InstallProgress.cs` — Appended 7 new phases; existing order preserved
- `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` — Added `WindrosePlusActive`/`WindrosePlusVersionTag` (defaulted)
- `src/WindroseServerManager.Core/Models/AppSettings.cs` — Added `WindrosePlusActiveByServer`, `WindrosePlusVersionByServer` dictionaries
- `tests/WindroseServerManager.Core.Tests/AppSettingsTests.cs` — Added `WindrosePlusActive_RoundTrip`
- `.planning/phases/08-windroseplus-bootstrap/08-VALIDATION.md` — `nyquist_compliant: true`, `wave_0_complete: true`, map rows resolved

## Decisions Made

1. **License filename:** `WindrosePlus-LICENSE.txt` at server-dir root — chosen for alphabetical grouping with `.wplus-version` and to make the WP-bundled artifacts visually cluster in Explorer.
2. **Expected `WindrosePlusService` constructor signature** (contract for Plan 02):
   ```csharp
   public WindrosePlusService(
       ILogger<WindrosePlusService> logger,
       IHttpClientFactory httpClientFactory,
       string cacheDir)
   ```
   — matches the commented-out instantiation in the plan's Task 3 action. Plan 02 must construct with exactly these parameters in this order; `logger` first (Microsoft convention), `cacheDir` last because it's the test-visible injection point.
3. **Per-server state keying:** Dictionaries in `AppSettings` are keyed by the server's full `InstallDir` path. `ServerInstallInfo` fields are derived (read-through) in Plan 02 — no double-persistence.
4. **`FakeGithubReleaseServer.FailWindrosePlusAsset` toggle added** (not in plan verbatim) — needed for `Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir` to simulate mid-download 500. Kept as public setter; documented in VALIDATION.md by test-row presence.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added `FailWindrosePlusAsset` toggle to FakeGithubReleaseServer**
- **Found during:** Task 2 (fixtures)
- **Issue:** Plan describes the test `Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir` as "inject a Tamper that returns valid metadata but asset 500" — the plan's fixture spec only had `TamperArchive` (byte mutation, not HTTP 500). Without a 500-simulation toggle, the test could not express the declared behavior.
- **Fix:** Added `public bool FailWindrosePlusAsset { get; set; }` to `FakeGithubReleaseServer`; when true, the WindrosePlus asset URL returns `HttpStatusCode.InternalServerError`.
- **Files modified:** `tests/WindroseServerManager.Core.Tests/Fixtures/FakeGithubReleaseServer.cs`
- **Verification:** Test file compiles; the Skip-guarded `Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir` uses `github.FailWindrosePlusAsset = true`.
- **Committed in:** `9e4f2be`

**2. [Rule 3 - Blocking] TestLogger implements non-generic ILogger**
- **Found during:** Task 3 (test stubs)
- **Issue:** Plan specified `TestLogger : ILogger<WindrosePlusService>`, but `WindrosePlusService` does not exist yet in Wave-0 — referencing it would break compilation.
- **Fix:** TestLogger implements the non-generic `Microsoft.Extensions.Logging.ILogger` instead. Plan 02 can still pass it as `ILogger<WindrosePlusService>` because `ILogger<T>` extends `ILogger` — any `ILogger` instance can be wrapped, and tests only assert `logger.Warnings` contents.
- **Files modified:** `tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs`
- **Verification:** Test project compiles green; `dotnet test` reports 55 passed / 13 skipped / 0 failed.
- **Committed in:** `149c491`

**3. [Rule 2 - Missing Critical] Exposed `RootDir` on TempServerFixture**
- **Found during:** Task 2
- **Issue:** Plan's Dispose logic used `Path.GetDirectoryName(ServerDir)!` to find the root — fragile if `ServerDir` structure ever changes.
- **Fix:** Added `public string RootDir { get; }` capturing the computed root; Dispose deletes `RootDir` directly.
- **Files modified:** `tests/WindroseServerManager.Core.Tests/Fixtures/TempServerFixture.cs`
- **Verification:** Tests skipped but fixture construction is non-throwing; no tests dispose-crash.
- **Committed in:** `9e4f2be`

---

**Total deviations:** 3 auto-fixed (1 missing-critical, 2 blocking)
**Impact on plan:** All three enable the declared Wave-0 surface to compile and stay green. No scope creep — no production code changed beyond the plan's `<action>` spec.

## Issues Encountered

- Pre-existing warnings in `ModServiceTests.cs` (CS0067, xUnit1031) surface during test build. Out of scope — not touched per scope boundary rule.
- Avalonia `AVLN5001` warning in `SettingsView.axaml` (TextBox.Watermark obsolete). Out of scope — belongs to App project, unrelated to Plan 08-01.

## Next Phase Readiness

- **Plan 02 (Wave 1)** is unblocked: create `WindrosePlusService.cs` with the constructor signature documented above, replace `null!` with `new WindrosePlusService(...)` in each test method, and delete the `Skip = SkipReason` argument from each `[Fact]`. All 13 behavior assertions are the target — when every test goes green without modification, WPLUS-01..04 are complete.
- **Plan 03 (Wave 2)** is partially blocked on Plan 02 but can begin scaffolding `ServerProcessServiceLauncherTests` against `IWindrosePlusService.ResolveLauncher` whose contract is final.
- No upstream decisions required. No user-setup needed.

## Self-Check: PASSED

Verified:
- All 10 created files exist at specified paths.
- Task commits `0327a9d`, `9e4f2be`, `149c491` exist in `git log`.
- `dotnet build` (solution): 0 errors.
- `dotnet test tests/WindroseServerManager.Core.Tests`: 55 passed / 13 skipped / 0 failed.
- `grep -c "public interface IWindrosePlusService"` in contract file = 1.
- `grep -c "Skip = SkipReason"` in WindrosePlusServiceTests.cs: 13 occurrences (via `const string SkipReason = ...`).

---
*Phase: 08-windroseplus-bootstrap*
*Completed: 2026-04-19*
