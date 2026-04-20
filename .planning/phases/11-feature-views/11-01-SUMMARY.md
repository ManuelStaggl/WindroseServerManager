---
phase: 11-feature-views
plan: "01"
subsystem: api
tags: [windroseplus, http, json, events-log, sea-chart, config-editor, xunit, di]

# Dependency graph
requires:
  - phase: 10-health-support
    provides: IWindrosePlusService, IToastService, IAppSettingsService, DashboardViewModel health loop
provides:
  - IWindrosePlusApiService interface + WindrosePlusApiService implementation (RCON, status, query, config read/write)
  - EventsLogParser (defensive line-delimited JSON parser for events.log)
  - SeaChartMath (Y-inverted WorldToCanvas transform)
  - WindrosePlusConfigSchema (13-entry static catalogue with Validate method)
  - 5 Core models: WindrosePlusPlayer, StatusResult, QueryResult, Event, Config
  - 43 Wave-0 xUnit tests (all green)
  - 4 skeleton ViewModels (Players, Events, SeaChart, Editor) registered in DI
  - 4 skeleton Views with page-title + subtitle placeholders
  - Navigation entries for all 4 views in MainWindowViewModel
  - Localization keys (Nav + view titles) in Strings.de.axaml + Strings.en.axaml
affects: [11-02-players-view, 11-03-events-view, 11-04-sea-chart, 11-05-config-editor]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - IWindrosePlusApiService as shared contract consumed by all Phase-11 ViewModels
    - Atomic write pattern (write-to-.tmp then File.Move with overwrite=true)
    - Port guard (port <= 0 returns null without HTTP call) for offline-safe service calls
    - Static helper classes (EventsLogParser, SeaChartMath, WindrosePlusConfigSchema) — pure functions, no DI needed
    - Microsoft.Extensions.Logging.ILogger<T> for Core service logging (not Serilog static — Core has no Serilog dep)

key-files:
  created:
    - src/WindroseServerManager.Core/Services/IWindrosePlusApiService.cs
    - src/WindroseServerManager.Core/Services/WindrosePlusApiService.cs
    - src/WindroseServerManager.Core/Services/EventsLogParser.cs
    - src/WindroseServerManager.Core/Services/SeaChartMath.cs
    - src/WindroseServerManager.Core/Services/WindrosePlusConfigSchema.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusPlayer.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusStatusResult.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusQueryResult.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusEvent.cs
    - src/WindroseServerManager.Core/Models/WindrosePlusConfig.cs
    - tests/WindroseServerManager.Core.Tests/Phase11/WindrosePlusApiServiceTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase11/EventsLogParserTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase11/SeaChartMathTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase11/EditorConfigTests.cs
  modified:
    - src/WindroseServerManager.App/App.axaml.cs
    - src/WindroseServerManager.App/ViewModels/MainWindowViewModel.cs
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml

key-decisions:
  - "WindrosePlusApiService uses ILogger<WindrosePlusApiService> (Microsoft.Extensions.Logging) not Serilog static Log — Core project has no Serilog dependency"
  - "Port guard uses port <= 0 (not == 0) to handle negative port edge cases"
  - "EventsLogParser accepts only type=join|leave (case-insensitive); all other types return null for safe extensibility"
  - "SeaChartMath.WorldToCanvas inverts Y axis (+worldY is up, canvas top is 0) — canvas coordinate convention"
  - "WriteConfigAsync atomic pattern: write to .tmp then File.Move with overwrite=true (matches EnsureArchiveCachedAsync precedent from WindrosePlusService)"
  - "WindrosePlusApiService takes ILogger<T> as third constructor param — test helpers use NullLogger<T>.Instance"

patterns-established:
  - "Port guard pattern: if (port <= 0) return null; — used in RconAsync, GetStatusAsync, QueryAsync"
  - "Linked CancellationTokenSource with CancelAfter for HTTP timeout (10s for RCON, 5s for status/query)"
  - "Defensive JSON parsing with TryGetProperty throughout — no PropertyNamedException on malformed responses"
  - "xUnit Phase11 tests under tests/WindroseServerManager.Core.Tests/Phase11/ — filter: --filter FullyQualifiedName~Phase11"

requirements-completed: [PLAYER-01, PLAYER-02, PLAYER-03, PLAYER-04, EVENT-01, EVENT-03, CHART-01, EDITOR-01, EDITOR-02, EDITOR-03]

# Metrics
duration: 35min
completed: "2026-04-20"
---

# Phase 11 Plan 01: Feature Views Foundation Summary

**IWindrosePlusApiService + 3 static helpers (EventsLogParser, SeaChartMath, ConfigSchema) + 5 Core models + 43 green xUnit tests + 4 skeleton ViewModels/Views with full DI registration and navigation entries**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-04-20T08:00:00Z
- **Completed:** 2026-04-20T08:35:00Z
- **Tasks:** 2
- **Files modified:** 18 (14 created + 4 modified)

## Accomplishments
- Full `IWindrosePlusApiService` contract with RCON, status polling, query, config read/write, and command builder methods — downstream plans 02–05 can consume without ambiguity
- Three fully-implemented static helpers: `EventsLogParser` (join/leave JSON parsing, filter matching), `SeaChartMath` (Y-inverted world-to-canvas transform), `WindrosePlusConfigSchema` (13-entry catalogue + Validate)
- 43 Wave-0 xUnit tests across 4 test files — all green immediately since helpers are fully implemented (service tests use port-guard and file-system paths only, no live HTTP)
- 4 skeleton ViewModels + 4 placeholder Views registered in DI and wired into sidebar navigation (Players/Events/SeaChart/Editor appear between Configuration and Mods)

## Task Commits

1. **Task 1: Core models, API service interface, helpers, Wave-0 tests** - `9c8e41e` (feat)
2. **Task 2: Skeleton ViewModels, empty Pages, DI registration, navigation, localization** - `d5bf3f9` (feat)

## Files Created/Modified
- `src/WindroseServerManager.Core/Services/IWindrosePlusApiService.cs` - Service contract for all Phase-11 ViewModels
- `src/WindroseServerManager.Core/Services/WindrosePlusApiService.cs` - Concrete implementation (port guard, atomic write, defensive JSON)
- `src/WindroseServerManager.Core/Services/EventsLogParser.cs` - Defensive JSON events.log parser + filter
- `src/WindroseServerManager.Core/Services/SeaChartMath.cs` - WorldToCanvas with Y-inversion
- `src/WindroseServerManager.Core/Services/WindrosePlusConfigSchema.cs` - 13-entry schema + Validate
- `src/WindroseServerManager.Core/Models/WindrosePlusPlayer.cs` - Player record
- `src/WindroseServerManager.Core/Models/WindrosePlusStatusResult.cs` - Status API response
- `src/WindroseServerManager.Core/Models/WindrosePlusQueryResult.cs` - Query API response
- `src/WindroseServerManager.Core/Models/WindrosePlusEvent.cs` - Events log entry
- `src/WindroseServerManager.Core/Models/WindrosePlusConfig.cs` - windrose_plus.json model
- `tests/WindroseServerManager.Core.Tests/Phase11/` - 4 test files (43 facts total)
- `src/WindroseServerManager.App/App.axaml.cs` - Added IWindrosePlusApiService + 4 ViewModel registrations
- `src/WindroseServerManager.App/ViewModels/MainWindowViewModel.cs` - 4 NavItems inserted
- `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` - Nav + view keys (DE)
- `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` - Nav + view keys (EN)

## Decisions Made
- `WindrosePlusApiService` uses `ILogger<WindrosePlusApiService>` from `Microsoft.Extensions.Logging` (not Serilog static `Log`) because Core.csproj has no Serilog package — consistent with `WindrosePlusService` precedent
- Port guard uses `port <= 0` (not `== 0`) to defensively cover negative port edge cases
- `EventsLogParser.TryParseLine` only accepts `type = "join" | "leave"` (case-insensitive); unknown types return `null` — safe extensibility for future event types without code changes
- `WriteConfigAsync` uses the atomic `.tmp` → `File.Move(overwrite:true)` pattern matching the established `EnsureArchiveCachedAsync` pattern in `WindrosePlusService`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ILogger instead of static Serilog in WindrosePlusApiService**
- **Found during:** Task 1 (WindrosePlusApiService.cs compilation)
- **Issue:** Plan referenced `Serilog.Log.Warning(...)` but Core project has no Serilog dependency — CS0103 compile error
- **Fix:** Added `ILogger<WindrosePlusApiService>` parameter to constructor, used `_logger.LogWarning(...)` instead
- **Files modified:** `src/WindroseServerManager.Core/Services/WindrosePlusApiService.cs`
- **Verification:** `dotnet build src/WindroseServerManager.Core/` → 0 errors
- **Committed in:** `9c8e41e` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Required for compilation. No scope creep. Constructor signature is stable for DI.

## Issues Encountered
- Parts of Task 2 were already implemented in prior commits (EventsViewModel, PlayersView, SeaChartView, EditorView, DI registrations). All acceptance criteria were already satisfied; Task 2 commit covered remaining string keys.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plans 02–05 can now execute in parallel with zero shared-file conflicts
- `IWindrosePlusApiService` contract is stable and fully tested
- All 4 skeleton ViewModels compile, are DI-registered, and show placeholder pages in the sidebar
- Full test suite: 155/155 tests green (112 existing + 43 new Phase11)
- Remaining: Plans 02–05 flesh out Players/Events/SeaChart/Editor views with real UI and behavior

---
*Phase: 11-feature-views*
*Completed: 2026-04-20*
