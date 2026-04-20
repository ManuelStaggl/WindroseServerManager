---
phase: 11-feature-views
plan: 05
subsystem: editor
tags: [config-editor, inline-validation, windrose-plus, avalonia]
dependency_graph:
  requires: [11-01, 11-04]
  provides: [EDITOR-01, EDITOR-02, EDITOR-03]
  affects: [EditorView, EditorViewModel, ConfigEntryViewModel, CategoryGroup, i18n]
tech_stack:
  added: []
  patterns:
    - "CategoryGroup for grouped ItemsControl binding"
    - "ConfigEntryViewModel with partial OnRawValueChanged for inline validation"
    - "WindrosePlusConfigSchema.Validate called on every RawValue change"
    - "Atomic WriteConfigAsync with restart-required toast when ServerStatus.Running"
key_files:
  created:
    - src/WindroseServerManager.App/ViewModels/ConfigEntryViewModel.cs
  modified:
    - src/WindroseServerManager.App/ViewModels/EditorViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/EditorView.axaml
    - src/WindroseServerManager.App/Views/Pages/EditorView.axaml.cs
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
decisions:
  - "CategoryGroup helper class defined in EditorViewModel.cs (same file) — no separate file needed for a simple data holder"
  - "OnAttachedToVisualTree triggers Start() in EditorView.axaml.cs — consistent with SeaChartView/EventsView pattern"
  - "FormatValue handles JsonElement (System.Text.Json deserializes Dictionary<string,object?> values as JsonElement) to display correct raw text"
  - "SaveCommand.CanExecute re-evaluated via NotifyCanExecuteChanged on each entry PropertyChanged(HasError)"
  - "Editor.Subtitle skeleton key from Plan 01 removed from both DE+EN string files"
metrics:
  duration: "~15 minutes"
  completed: "2026-04-20"
  tasks_completed: 2
  files_modified: 6
---

# Phase 11 Plan 05: Config Editor Summary

Config Editor with grouped inline-validated entry form for windrose_plus.json, atomic save, and restart-required toast when server is running.

## What Was Built

### ConfigEntryViewModel
Per-entry VM that holds `RawValue` (two-way bound `TextBox`), triggers `WindrosePlusConfigSchema.Validate` on every keystroke via `partial void OnRawValueChanged`, and exposes `HasError` / `ErrorMessage` for inline display. `ToTypedValue()` converts the raw string back to the correct JSON type (float, int, bool, string) using InvariantCulture.

`FormatValue` special-cases `System.Text.Json.JsonElement` — the type returned when deserializing `Dictionary<string, object?>` via STJ — so existing config values are shown correctly.

### EditorViewModel
Loads `windrose_plus.json` via `IWindrosePlusApiService.ReadConfig`, groups all 13 schema-defined entries by Category (Server / Multipliers), and builds `ObservableCollection<CategoryGroup>`. Each `ConfigEntryViewModel` subscribes to `PropertyChanged` to keep `CanSave` / `HasAnyError` up-to-date and call `SaveCommand.NotifyCanExecuteChanged`.

`SaveCommand` is gated via `CanExecute = nameof(CanExecuteSave)` (returns `CanSave = !HasAnyError && !IsLoading`). On success: writes via `WriteConfigAsync`, shows `Editor.Saved` toast, then conditionally shows `Editor.RestartRequired` warning toast if `_proc.Status == ServerStatus.Running`.

### EditorView.axaml
Outer `Grid` with three rows: page title, `ScrollViewer > ItemsControl` grouped by `CategoryGroup`, footer with validation summary + Save button. Inner `ItemsControl` per group renders `(Label | TextBox | ErrorMessage)` in a 3-column `Grid`. Error text uses `BrandErrorBrush` matching the project convention, visible only when `HasError`.

### i18n
7 new string keys in DE + EN:
- `Editor.Save` / `Editor.Saved` / `Editor.RestartRequired`
- `Editor.ValidationError` / `Editor.Error.Load` / `Editor.Error.Save`
- Skeleton `Editor.Subtitle` from Plan 01 removed.

## Deviations from Plan

None — plan executed exactly as written. `ServerStatus.Running` confirmed correct enum member before writing. `BrandErrorBrush` confirmed from `WindrosePlusOptInControl.axaml` usage.

## Verification

- `dotnet build`: 0 errors, 32 pre-existing warnings
- `dotnet test`: 155/155 passed

## Self-Check: PASSED

Files confirmed:
- `src/WindroseServerManager.App/ViewModels/ConfigEntryViewModel.cs` — contains `WindrosePlusConfigSchema.Validate`, `partial void OnRawValueChanged`, `ToTypedValue`
- `src/WindroseServerManager.App/ViewModels/EditorViewModel.cs` — contains `ReadConfig`, `WriteConfigAsync`, `ServerStatus.Running`, `RestartRequired`, `class CategoryGroup`
- `src/WindroseServerManager.App/Views/Pages/EditorView.axaml` — contains `ItemsControl`, `SaveCommand`, `ErrorMessage`, `CategoryGroup`
- Strings EN — contains `Editor.RestartRequired`, `Editor.Saved`
- Strings DE — contains `Server muss neu gestartet werden`

Commits:
- `25d09da` — feat(11-05): ConfigEntryViewModel + EditorViewModel with save/restart-prompt
- `f158d58` — feat(11-05): EditorView grouped editor UI + i18n strings DE+EN
