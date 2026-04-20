---
phase: 11-feature-views
plan: "03"
subsystem: events-view
tags: [avalonia, events-log, file-watcher, tail-read, filter, i18n]
dependency_graph:
  requires: [11-01, 11-02]
  provides: [events-view-end-to-end]
  affects: [MainWindowViewModel, App.axaml.cs, Strings]
tech_stack:
  added: [FileSystemWatcher, SemaphoreSlim, Avalonia.Threading.Dispatcher.UIThread]
  patterns: [tail-read, debounce, ObservableCollection-filter, ViewLocator-mapping]
key_files:
  created:
    - src/WindroseServerManager.App/ViewModels/EventsViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/EventsView.axaml
    - src/WindroseServerManager.App/Views/Pages/EventsView.axaml.cs
  modified:
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/App.axaml.cs
    - src/WindroseServerManager.App/ViewModels/MainWindowViewModel.cs
decisions:
  - "EnableRowVirtualization removed — Avalonia DataGrid does not support this WPF property; row virtualization is active by default in Avalonia"
  - "EventsViewModel constructor takes IWindrosePlusApiService + IAppSettingsService — Api service reserved for future kick/ban integration, not used in this plan"
  - "Skeleton ViewModels (PlayersViewModel, SeaChartViewModel, EditorViewModel) and their Views committed in this plan because MainWindowViewModel (linter-updated) references them — keeps build green without waiting for Plans 11-02/11-04/11-05"
metrics:
  duration_seconds: 239
  completed_date: "2026-04-20"
  tasks_completed: 2
  files_changed: 7
  files_created: 4
---

# Phase 11 Plan 03: Events View Summary

**One-liner:** Live event log with FileSystemWatcher tail-read, 150ms debounced incremental append, in-memory filter via EventsLogParser, and Avalonia DataGrid row virtualization for 1000+ entries.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | EventsViewModel — FileSystemWatcher + tail-read + debounced filter | 0ad6767 | EventsViewModel.cs |
| 2 | EventsView.axaml + code-behind + i18n strings + nav integration | ff459a3 | EventsView.axaml, EventsView.axaml.cs, Strings.*.axaml, App.axaml.cs, MainWindowViewModel.cs |

## What Was Built

### EventsViewModel (Task 1)

Full lifecycle management:
- `Start()` — resolves `{serverDir}/windrose_plus_data/events.log`, runs `InitialLoadAsync` as fire-and-forget Task, arms FileSystemWatcher
- `InitialLoadAsync` — reads entire log with `FileShare.ReadWrite`, tracks `_lastReadPosition`
- `OnLogChanged` — debounce handler: cancels previous CancellationTokenSource, creates new one, waits 150ms before calling `ReadNewLinesAsync`
- `ReadNewLinesAsync` — seeks to `_lastReadPosition`, reads only new bytes, detects log rotation via `info.Length < _lastReadPosition`
- `RebuildFilter` — thread-safe: checks `Dispatcher.UIThread.CheckAccess()`, posts to UI thread if needed; applies `EventsLogParser.MatchesFilter` to rebuild `FilteredEvents`
- `Stop()` — disables FSW, unsubscribes events, disposes; `Dispose()` calls `Stop()` + cleans semaphore
- `SemaphoreSlim(1,1)` serializes concurrent reads (prevents races on rapid FSW events)

### EventsView.axaml (Task 2)

Layout: `Grid RowDefinitions="Auto,Auto,*"` — title row, filter TextBox, DataGrid in a bordered panel.

DataGrid: bound to `FilteredEvents`, 4 columns (Time `yyyy-MM-dd HH:mm:ss`, Type, Player, Steam ID). Avalonia DataGrid has row virtualization enabled by default (no `EnableRowVirtualization` property in Avalonia API).

Empty state: `StackPanel IsVisible="{Binding HasNoEvents}"` overlaid on DataGrid.

### EventsView.axaml.cs

Overrides `OnAttachedToVisualTree` → calls `Start()`, `OnDetachedFromVisualTree` → calls `Stop()`. This ensures watcher is armed only while the view is visible and released on navigation away.

### i18n Strings

7 Events.* keys added to both `Strings.en.axaml` and `Strings.de.axaml`:
`Events.Title`, `Events.Column.Time/Type/Player/SteamId`, `Events.Filter.Placeholder`, `Events.Empty`

4 Nav.* keys also added: `Nav.Players`, `Nav.Events`, `Nav.SeaChart`, `Nav.Editor`

### DI + Nav Registration

- `App.axaml.cs`: `EventsViewModel` registered as Singleton; `IWindrosePlusApiService / WindrosePlusApiService` registration was added by linter
- `MainWindowViewModel`: Nav entries for Players, Events, SeaChart, Editor added (linter update added all 4 Phase-11 entries simultaneously)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Avalonia DataGrid has no EnableRowVirtualization property**
- **Found during:** Task 2 — dotnet build error AVLN2000
- **Issue:** Plan used WPF property `EnableRowVirtualization="True"` — not valid in Avalonia DataGrid
- **Fix:** Removed the attribute; added XML comment documenting that Avalonia DataGrid enables virtualization by default
- **Files modified:** EventsView.axaml
- **Commit:** ff459a3

**2. [Rule 3 - Blocking] Skeleton ViewModels/Views for Plans 11-02/11-04/11-05 committed**
- **Found during:** Task 2 — linter auto-updated MainWindowViewModel to include all 4 Phase-11 nav entries (Players, Events, SeaChart, Editor), causing compile errors for missing types
- **Fix:** Staged and committed the pre-existing skeleton files (PlayersViewModel, SeaChartViewModel, EditorViewModel and their View stubs) to keep build green; these files were already created by prior plan setup, just not tracked in git yet
- **Files modified:** 9 skeleton files added to the commit
- **Commit:** ff459a3

## Verification Results

- `dotnet build`: 0 errors, 30 warnings (all pre-existing CA1416 + AVLN5001)
- `dotnet test`: 155/155 passed, 0 skipped

## Acceptance Criteria Check

| Criterion | Result |
|-----------|--------|
| EventsViewModel.cs contains `FileSystemWatcher` | PASS |
| EventsViewModel.cs contains `_lastReadPosition` | PASS |
| EventsViewModel.cs contains `EventsLogParser.TryParseLine` | PASS |
| EventsViewModel.cs contains `EventsLogParser.MatchesFilter` | PASS |
| EventsViewModel.cs contains `Dispatcher.UIThread` | PASS |
| EventsViewModel.cs contains `partial void OnFilterTextChanged` | PASS |
| EventsViewModel.cs contains `public void Dispose()` and `: IDisposable` | PASS |
| EventsView.axaml contains row virtualization (Avalonia default; comment added) | PASS |
| EventsView.axaml contains `FilteredEvents` | PASS |
| EventsView.axaml contains `FilterText` | PASS |
| EventsView.axaml.cs contains `(DataContext as EventsViewModel)?.Start()` | PASS |
| Strings.en.axaml contains `Events.Filter.Placeholder` | PASS |
| Strings.de.axaml contains `Ereignislog` | PASS |
| dotnet build exits 0 | PASS |
| dotnet test exits 0 | PASS |

## Self-Check: PASSED

- EventsViewModel.cs: FOUND
- EventsView.axaml: FOUND
- EventsView.axaml.cs: FOUND
- Commit 0ad6767: FOUND
- Commit ff459a3: FOUND
