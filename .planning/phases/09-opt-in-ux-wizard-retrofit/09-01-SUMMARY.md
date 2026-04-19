---
phase: 09-opt-in-ux-wizard-retrofit
plan: 01
subsystem: core-foundation
tags: [appsettings, opt-in, migration, steamid, rcon, tcp-probe, json-serialization]

# Dependency graph
requires:
  - phase: 08-windroseplus-bootstrap
    provides: WindrosePlusActiveByServer / WindrosePlusVersionByServer dicts keyed by InstallDir; ServerInstallInfo read-through pattern; WindrosePlusService contract
provides:
  - OptInState enum (NeverAsked / OptedIn / OptedOut) with JsonStringEnumConverter
  - 4 new AppSettings per-server dicts (RconPassword, DashboardPort, AdminSteamId, OptInState)
  - ServerInstallInfo gains 4 read-through positional parameters mirroring the new dicts
  - RconPasswordGenerator (URL-safe, 24+ chars, RandomNumberGenerator-backed)
  - SteamIdParser (raw 17-digit + /profiles/ URL, vanity rejected, 5s regex timeout)
  - FreePortProbe (18080..18099 with OS ephemeral fallback via TcpListener)
  - AppSettingsService.MigrateToV12 idempotent seeder run inside LoadAsync
affects: [09-02-wizard, 09-03-retrofit, 10-health, 11-feature-views, 12-empty-states]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - JsonStringEnumConverter at enum declaration for human-readable persisted values
    - Per-server state stored as parallel dictionaries keyed by full InstallDir path (consistent with Phase 8)
    - ServerInstallInfo remains a read-through view over AppSettings — no double-persistence
    - Migration runs synchronously inside LoadAsync, before Current is exposed — no UI race
    - Idempotent migration via ContainsKey guard — never overwrites OptedIn/OptedOut

key-files:
  created:
    - src/WindroseServerManager.Core/Models/OptInState.cs
    - src/WindroseServerManager.Core/Services/RconPasswordGenerator.cs
    - src/WindroseServerManager.Core/Services/SteamIdParser.cs
    - src/WindroseServerManager.Core/Services/FreePortProbe.cs
    - tests/WindroseServerManager.Core.Tests/Phase9/AppSettingsPhase9Tests.cs
    - tests/WindroseServerManager.Core.Tests/Phase9/RconPasswordGeneratorTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase9/SteamIdParserTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase9/FreePortProbeTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase9/OptInMigrationTests.cs
  modified:
    - src/WindroseServerManager.Core/Models/AppSettings.cs
    - src/WindroseServerManager.Core/Models/ServerInstallInfo.cs
    - src/WindroseServerManager.Core/Services/AppSettingsService.cs

key-decisions:
  - "OptInState serialized as string (JsonStringEnumConverter on the enum) — human-readable settings.json is the existing project convention"
  - "Added second public AppSettingsService ctor accepting an explicit settings path — unblocks deterministic migration tests without an InternalsVisibleTo attribute; the primary %AppData% ctor remains the DI entry point and is untouched"
  - "Migration retains orphan OptInState keys (server removed from WindrosePlusActiveByServer but decision persists) — Phase 9 is strictly non-destructive; cleanup is out of scope"
  - "ServerInstallInfo.NotInstalled factory left with its original 5-arg call — all 6 new parameters have defaults, so the factory body needed no change"
  - "FreePortProbe fallback uses TcpListener(IPAddress.Loopback, 0) and stops immediately — the OS-assigned port is released before the probe returns, matching the 'caller must bind immediately' contract"

patterns-established:
  - "Phase 9 test suite lives under tests/.../Phase9/ and is selected via --filter FullyQualifiedName~Phase9"
  - "Tests that need to drive AppSettingsService against a temp settings.json use the new (logger, path) ctor + IDisposable fixture for cleanup"
  - "Enum persistence: decorate the enum type itself with JsonStringEnumConverter — property-level attributes would need repeating on every Dictionary<,> declaration"

requirements-completed: [WIZARD-03, WIZARD-04, RETRO-01]

# Metrics
duration: 3m 17s
completed: 2026-04-19
---

# Phase 9 Plan 01: Foundation Summary

**Opt-in persistence model (4 per-server dicts + OptInState enum), three stateless helper utilities (RCON password gen, SteamID64 extractor, free-port probe), and an idempotent LoadAsync migration that seeds NeverAsked for every known server exactly once.**

## Performance

- **Duration:** 3 min 17 s
- **Started:** 2026-04-19T18:07:01Z
- **Completed:** 2026-04-19T18:10:18Z
- **Tasks:** 3 (all TDD, all green on first run)
- **Files created:** 9
- **Files modified:** 3

## Accomplishments

- Full Phase 9 persistence contract frozen: `OptInState` enum + 4 AppSettings dicts + 4 ServerInstallInfo read-through properties. Plans 02 and 03 now consume this without touching the model again.
- Three independent static helper services — `RconPasswordGenerator`, `SteamIdParser`, `FreePortProbe` — live under `WindroseServerManager.Core.Services` with zero cross-dependencies. Each is fully covered by unit tests and can be called from ViewModels verbatim.
- `AppSettingsService.LoadAsync` now runs `MigrateToV12` synchronously before exposing `Current`. Existing settings.json files gain `NeverAsked` for every server in `WindrosePlusActiveByServer`; pre-existing `OptedIn`/`OptedOut` decisions survive re-load unchanged; orphan keys are retained.
- Phase 9 test slice: **34 tests**, runs in ~100 ms. Full suite: **105 / 105 green**.

## Task Commits

1. **Task 1: OptInState enum + AppSettings + ServerInstallInfo extensions** — `168952e` (feat)
2. **Task 2: Phase-9 helpers (RconPasswordGenerator, SteamIdParser, FreePortProbe) + tests** — `bbed2c8` (feat)
3. **Task 3: Opt-in migration in AppSettingsService.LoadAsync + migration tests** — `2e3c7f5` (feat)

_Note: all three tasks followed TDD — tests and implementation co-committed because helpers are pure and round-trip/migration invariants are the behaviour contract._

## Files Created/Modified

### Created
- `src/WindroseServerManager.Core/Models/OptInState.cs` — 3-value enum with `[JsonConverter(typeof(JsonStringEnumConverter))]`
- `src/WindroseServerManager.Core/Services/RconPasswordGenerator.cs` — `Generate(int length = 24)` over `RandomNumberGenerator.GetBytes`
- `src/WindroseServerManager.Core/Services/SteamIdParser.cs` — two compiled regexes with 5 s timeout, trim + raw/URL match
- `src/WindroseServerManager.Core/Services/FreePortProbe.cs` — preferred-range loop + OS ephemeral fallback
- `tests/.../Phase9/AppSettingsPhase9Tests.cs` — 5 tests (enum, dicts, round-trip, record defaults/positional)
- `tests/.../Phase9/RconPasswordGeneratorTests.cs` — 5 tests
- `tests/.../Phase9/SteamIdParserTests.cs` — 14 theory cases
- `tests/.../Phase9/FreePortProbeTests.cs` — 3 tests (positive port, rebindable, fallback outside range)
- `tests/.../Phase9/OptInMigrationTests.cs` — 7 tests (seed, idempotent OptedOut/OptedIn, empty, sync visibility, orphan retention, no-file init)

### Modified
- `src/WindroseServerManager.Core/Models/AppSettings.cs` — +4 Phase 9 dicts under the existing `// WindrosePlus` block
- `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` — record param list extended by 4 defaulted positional args
- `src/WindroseServerManager.Core/Services/AppSettingsService.cs` — `MigrateToV12` private static + two call sites (no-file and post-deserialize) + second public ctor `(ILogger, string settingsPath)` for deterministic tests

## Positional Parameter Order — ServerInstallInfo (for Plans 02 & 03)

Downstream plans consuming the record MUST honour this order:

```csharp
new ServerInstallInfo(
    IsInstalled,
    InstallDir,
    BuildId,
    SizeBytes,
    LastUpdatedUtc,
    WindrosePlusActive = false,
    WindrosePlusVersionTag = null,
    WindrosePlusRconPassword = null,   // NEW
    WindrosePlusDashboardPort = 0,     // NEW
    WindrosePlusAdminSteamId = null,   // NEW
    WindrosePlusOptInState = OptInState.NeverAsked)  // NEW
```

All four new params have defaults, so existing call sites (including `NotInstalled`) compile unchanged.

## Decisions Made

See frontmatter `key-decisions`. The two non-obvious ones:

1. **Second public ctor on `AppSettingsService`** — the existing ctor hard-codes `%AppData%\WindroseServerManager\settings.json`, which makes migration behaviour untestable without touching the user's real settings. Rather than introduce `InternalsVisibleTo` or restructure the path wiring, a second `public AppSettingsService(ILogger, string settingsPath)` ctor accepts an explicit path. DI still calls the single-arg ctor; the new one exists only for tests and future forks (e.g., portable mode).
2. **Orphan key retention** — a key present in `WindrosePlusOptInStateByServer` but missing from `WindrosePlusActiveByServer` (server deleted after user made a decision) is **kept**. Rationale: if the user later restores the same InstallDir, their previous `OptedIn`/`OptedOut` decision should resurface rather than silently revert to `NeverAsked`. Cleanup, if ever desired, belongs in a later explicit pass — not in the foundation migration.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 – Blocking] AppSettingsService had no test-friendly constructor**
- **Found during:** Task 3 (migration tests)
- **Issue:** The production ctor hard-codes `%AppData%\WindroseServerManager\settings.json`. The plan's test strategy ("pass a unique `Path.Combine(Path.GetTempPath(), Guid.NewGuid())`") presumed a path-accepting ctor that did not exist.
- **Fix:** Added a second public ctor `AppSettingsService(ILogger<AppSettingsService> logger, string settingsPath)` that skips the legacy-path migration. Production DI path is untouched — only tests (and future portable-mode callers) use it.
- **Files modified:** `src/WindroseServerManager.Core/Services/AppSettingsService.cs`
- **Verification:** 7 migration tests green; full suite 105/105 green.
- **Committed in:** `2e3c7f5` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Minimal public-surface expansion (one extra ctor). `IAppSettingsService` is untouched. No scope creep.

## Issues Encountered

- **Bash `/p:` quoting:** first `dotnet build` invocation had `/p:TreatWarningsAsErrors=false` flattened into args by the shell (MSBuild error MSB1008). Re-ran with `-p:` form — clean build. Not a code issue, noted only for future shell invocations on this Windows host.
- **JSON backward-compat concerns during migration tests:** none hit. Existing `WindrosePlusActiveByServer` / `WindrosePlusVersionByServer` dicts deserialize as before; the four new dicts are missing from pre-v1.2 JSON and default to `new()` via property initializers — no reader exception, no data loss.

## Next Phase Readiness

**Ready for Plan 02 (wizard):**
- `AppSettings` now carries every field the wizard needs to persist (RCON password, port, admin SteamID, OptInState).
- `ServerInstallInfo` surfaces them read-through — wizard ViewModel can hydrate a single record and write via `AppSettingsService.UpdateAsync`.
- `RconPasswordGenerator.Generate()` / `FreePortProbe.FindFreePort()` / `SteamIdParser.ExtractSteamId64()` are directly callable from the wizard ViewModel.

**Ready for Plan 03 (retrofit):**
- Migration guarantees every existing server has `OptInState = NeverAsked` on first v1.2 launch.
- `DashboardViewModel`'s 2 s retrofit-banner timer can safely read `WindrosePlusOptInStateByServer[InstallDir]` without any null-or-missing fallback.

**No blockers.** No UI changes were made in this plan — Plan 02 begins the Avalonia wizard work with a stable data contract underneath.

## Self-Check

Verification ran after summary draft:
- `src/WindroseServerManager.Core/Models/OptInState.cs` FOUND
- `src/WindroseServerManager.Core/Services/RconPasswordGenerator.cs` FOUND
- `src/WindroseServerManager.Core/Services/SteamIdParser.cs` FOUND
- `src/WindroseServerManager.Core/Services/FreePortProbe.cs` FOUND
- `tests/.../Phase9/*Tests.cs` (5 files) FOUND
- Commits `168952e`, `bbed2c8`, `2e3c7f5` FOUND in `git log`

## Self-Check: PASSED

---
*Phase: 09-opt-in-ux-wizard-retrofit*
*Completed: 2026-04-19*
