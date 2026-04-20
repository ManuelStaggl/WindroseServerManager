---
phase: 11-feature-views
plan: 02
subsystem: ui
tags: [avalonia, wpf, players, rcon, windrose-plus, datagrid, polling]

requires:
  - phase: 11-01
    provides: IWindrosePlusApiService (GetStatusAsync, RconAsync, BuildKickCommand, BuildBanCommand, BuildBroadcastCommand), WindrosePlusPlayer model, PlayersViewModel skeleton

provides:
  - PlayersViewModel: full poll timer (System.Timers.Timer), DiffUpdate, KickAsync/BanAsync/BroadcastAsync RelayCommands, IDisposable lifecycle
  - BanDialog: Permanent/Timed modal with NumericUpDown, BanDialogResult record, static ShowAsync
  - PlayersView: DataGrid (Name/SteamId/Alive/Session), error banner, empty state, kick/ban/broadcast toolbar
  - 22 Players.* i18n string keys in both DE and EN
  - AppSettings.WindrosePlusPlayerRefreshSeconds property

affects: [11-05, phase-12]

tech-stack:
  added: []
  patterns:
    - "System.Timers.Timer poll with Dispatcher.UIThread.InvokeAsync for ObservableCollection updates"
    - "DiffUpdate pattern: remove stale, update existing, add new — avoids full list clear on refresh"
    - "BanDialog mirrors ConfirmDialog: static ShowAsync returns strongly-typed result record"
    - "OnAttachedToVisualTree/OnDetachedFromVisualTree for Start/Stop lifecycle in UserControl code-behind"

key-files:
  created:
    - src/WindroseServerManager.App/Views/Dialogs/BanDialog.axaml
    - src/WindroseServerManager.App/Views/Dialogs/BanDialog.axaml.cs
  modified:
    - src/WindroseServerManager.App/ViewModels/PlayersViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/PlayersView.axaml
    - src/WindroseServerManager.App/Views/Pages/PlayersView.axaml.cs
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
    - src/WindroseServerManager.Core/Models/AppSettings.cs

key-decisions:
  - "DiffUpdate avoids clearing ObservableCollection on each poll tick — prevents selection loss and reduces UI jank"
  - "BanDialogResult(int? Minutes) record: null=permanent, int=timed duration — clean discriminated-union pattern without enum"
  - "Watermark attribute kept on TextBox (matching project-wide convention) instead of PlaceholderText — avoids unnecessary divergence"

patterns-established:
  - "Dialog ShowAsync pattern: static factory method returns strongly-typed result record"
  - "Poll VM lifecycle: Start/Stop driven by visual tree attachment events in code-behind"

requirements-completed: [PLAYER-01, PLAYER-02, PLAYER-03, PLAYER-04]

duration: 15min
completed: 2026-04-20
---

# Phase 11 Plan 02: Players View Summary

**Live player list with configurable 10s poll timer, kick/ban RCON commands gated by ConfirmDialog/BanDialog (permanent + timed modes), and broadcast send — all wired to IWindrosePlusApiService**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-04-20T05:00:00Z
- **Completed:** 2026-04-20T05:15:00Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Full PlayersViewModel with System.Timers.Timer poll, DiffUpdate (avoids list-clear flicker), KickAsync/BanAsync/BroadcastAsync RelayCommands, and IDisposable lifecycle
- BanDialog with Permanent/Timed radio toggle, NumericUpDown (1–525600 minutes), static ShowAsync returning BanDialogResult? (null=cancelled)
- PlayersView DataGrid with Name/SteamId/Alive/Session columns, error banner, empty state, kick/ban/broadcast toolbar
- 22 Players.* i18n string keys in both DE and EN (including all BanDialog, Kick confirm, and toast strings)

## Task Commits

Each task was committed atomically:

1. **Task 1: PlayersViewModel + BanDialog + AppSettings** - `6a86fe5` (feat)
2. **Task 2: PlayersView full UI + strings DE/EN** - `c2186ec` (feat)

## Files Created/Modified
- `src/WindroseServerManager.App/Views/Dialogs/BanDialog.axaml` - Permanent/Timed ban dialog UI
- `src/WindroseServerManager.App/Views/Dialogs/BanDialog.axaml.cs` - BanDialogResult record + ShowAsync
- `src/WindroseServerManager.App/ViewModels/PlayersViewModel.cs` - Full VM with poll timer + 4 RelayCommands
- `src/WindroseServerManager.App/Views/Pages/PlayersView.axaml` - Full DataGrid UI with toolbar
- `src/WindroseServerManager.App/Views/Pages/PlayersView.axaml.cs` - Start/Stop lifecycle in attach/detach overrides
- `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` - 22 Players.* keys added (DE)
- `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` - 22 Players.* keys added (EN)
- `src/WindroseServerManager.Core/Models/AppSettings.cs` - WindrosePlusPlayerRefreshSeconds property

## Decisions Made
- DiffUpdate avoids ObservableCollection.Clear() on each poll to preserve selection and reduce UI jank
- BanDialogResult(int? Minutes) record: null=permanent, int=timed — clean pattern matching in BanAsync
- Watermark kept on TextBox (project-wide convention) instead of PlaceholderText

## Deviations from Plan

None — plan executed exactly as written. Task 1 artifacts (AppSettings property, PlayersViewModel, BanDialog) were already partially present from Plan 01 skeleton commits; this plan completed and wired them fully.

## Issues Encountered
None — build and all 155 tests passed on first attempt.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- PLAYER-01 through PLAYER-04 satisfied end-to-end
- Players view live: DataGrid, kick/ban dialogs, broadcast send
- Plan 05 (Config Editor) is the remaining Phase 11 plan

---
*Phase: 11-feature-views*
*Completed: 2026-04-20*
