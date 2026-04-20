---
phase: 11-feature-views
plan: "04"
subsystem: ui
tags: [avalonia, canvas, mvvm, seachart, windroseplus, polling]

# Dependency graph
requires:
  - phase: 11-01
    provides: IWindrosePlusApiService.QueryAsync, SeaChartMath.WorldToCanvas, WindrosePlusPlayer model
  - phase: 11-03
    provides: Events view pattern for code-behind lifecycle (Start/Stop)
provides:
  - Canvas-based sea-chart view with live player position markers
  - PlayerMarkerViewModel with CanvasX/CanvasY/SelectCommand
  - SeaChartViewModel with 5s poll timer, auto-expanding world bounds, marker diff
  - Inline detail panel for selected player (name, SteamId, alive, ship info)
  - Generate Map button firing wp.mapgen RCON + polling for map.png
affects: [11-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Canvas-based ItemsControl with ItemContainerTheme setting Canvas.Left/Top from ViewModel"
    - "Auto-expanding world bounds: start at ±30000, grow as player data arrives"
    - "Marker diffing by SteamId: update existing, add new, remove departed — preserves selection"
    - "PointerPressed on Ellipse routed to SelectCommand via code-behind"
    - "IDisposable ViewModel: Timer.Stop + CTS.Cancel + Bitmap.Dispose in Dispose()"

key-files:
  created:
    - src/WindroseServerManager.App/ViewModels/PlayerMarkerViewModel.cs
    - src/WindroseServerManager.App/ViewModels/SeaChartViewModel.cs
  modified:
    - src/WindroseServerManager.App/Views/Pages/SeaChartView.axaml
    - src/WindroseServerManager.App/Views/Pages/SeaChartView.axaml.cs
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml

key-decisions:
  - "PointerPressed instead of TapGestureRecognizer: TapGestureRecognizer caused AVLN2000 namespace resolution error in Avalonia"
  - "x:DataType on ControlTheme for Canvas.Left/Top bindings: required for Avalonia compiled binding validation against PlayerMarkerViewModel"
  - "Toggle-on-same-marker deselect: clicking the same marker again closes the detail panel (ReferenceEquals check)"
  - "Auto-expanding bounds start at ±30000: low confidence on actual Windrose world extent, grows with data"

patterns-established:
  - "Marker centering: offset -6px on both axes to center a 12×12 ellipse on the world position"
  - "Canvas SizeChanged wired in OnAttachedToVisualTree, vm.Start()/Stop() lifecycle tied to visual tree"

requirements-completed: [CHART-01, CHART-02]

# Metrics
duration: 25min
completed: 2026-04-20
---

# Phase 11 Plan 04: Sea-Chart Summary

**Canvas-based player map with 5s polling, auto-expanding world-space bounds, marker diff by SteamId, and clickable inline detail panel**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-04-20T04:20:00Z
- **Completed:** 2026-04-20T04:44:07Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- PlayerMarkerViewModel with CanvasX/CanvasY computed from WorldToCanvas, SelectCommand toggle
- SeaChartViewModel: 5s poll timer via System.Timers.Timer, auto-expanding world bounds, DiffMarkers (update-in-place by SteamId), GenerateMap RCON + map.png polling
- SeaChartView.axaml: Canvas with map image + ItemsControl of Ellipse markers + detail panel visible when SelectedMarker != null
- Code-behind wires canvas SizeChanged to UpdateCanvasSize and routes PointerPressed to SelectCommand
- i18n strings added for SeaChart.* keys in both EN and DE

## Task Commits

Each task was committed atomically:

1. **Task 1: PlayerMarkerViewModel + SeaChartViewModel** - `c06bc75` (feat)
2. **Task 2: SeaChartView code-behind with canvas size tracking and marker tap handler** - `39bec21` (feat)

Note: SeaChartView.axaml XAML was committed as part of plan 11-01 skeleton (`502cc30`), then updated with ControlTheme fix (`cac7f29`).

## Files Created/Modified
- `src/WindroseServerManager.App/ViewModels/PlayerMarkerViewModel.cs` - Single marker VM with CanvasX, CanvasY, SelectCommand
- `src/WindroseServerManager.App/ViewModels/SeaChartViewModel.cs` - Full poll timer + marker diff + detail panel VM
- `src/WindroseServerManager.App/Views/Pages/SeaChartView.axaml` - Canvas with map image, ItemsControl of markers, detail panel
- `src/WindroseServerManager.App/Views/Pages/SeaChartView.axaml.cs` - Start/Stop lifecycle, SizeChanged tracking, PointerPressed routing
- `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` - SeaChart.* EN strings
- `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` - SeaChart.* DE strings

## Decisions Made
- **PointerPressed over TapGestureRecognizer:** TapGestureRecognizer caused Avalonia compile error AVLN2000 (type could not be resolved). PointerPressed directly on the Ellipse element achieves the same result cleanly.
- **x:DataType on ControlTheme:** The Canvas.Left/Top setters in ItemContainerTheme need `x:DataType="vm:PlayerMarkerViewModel"` for Avalonia's compiled binding engine to resolve CanvasX/CanvasY correctly.
- **Toggle deselect on repeat click:** `ReferenceEquals(SelectedMarker, marker) ? null : marker` gives natural UX — tapping the same dot closes the panel.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] TapGestureRecognizer replaced with PointerPressed**
- **Found during:** Task 2 (SeaChartView XAML)
- **Issue:** `<TapGestureRecognizer>` caused Avalonia compile error AVLN2000 — type not resolvable in this context
- **Fix:** Removed GestureRecognizers block, added `PointerPressed="OnMarkerPointerPressed"` directly on Ellipse; handler renamed accordingly
- **Files modified:** SeaChartView.axaml, SeaChartView.axaml.cs
- **Verification:** dotnet build exits 0 with 0 errors
- **Committed in:** cac7f29

**2. [Rule 1 - Bug] x:DataType added to ControlTheme for Canvas position bindings**
- **Found during:** Task 2 (SeaChartView XAML binding validation)
- **Issue:** Without x:DataType on ControlTheme, Avalonia's compiled binding engine couldn't resolve CanvasX/CanvasY against SeaChartViewModel (wrong scope)
- **Fix:** Added `x:DataType="vm:PlayerMarkerViewModel"` to the ControlTheme
- **Files modified:** SeaChartView.axaml
- **Verification:** dotnet build exits 0 with 0 errors
- **Committed in:** cac7f29

---

**Total deviations:** 2 auto-fixed (both Rule 1 - bug)
**Impact on plan:** Both fixes required for correct binding and compilation. No scope creep.

## Issues Encountered
- `Loc` static class not found until `using WindroseServerManager.App.Services;` added to SeaChartViewModel — resolved immediately.
- SeaChartView.axaml skeleton was committed in plan 11-01 execution; the current plan updated it rather than created it fresh.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Sea-Chart view fully implemented and compiling (155 tests pass, 0 build errors)
- Plan 11-05 (Config Editor) can proceed — no blockers from this plan
- Manual smoke test with live WindrosePlus server still needed to verify markers appear and detail panel opens

---
*Phase: 11-feature-views*
*Completed: 2026-04-20*
