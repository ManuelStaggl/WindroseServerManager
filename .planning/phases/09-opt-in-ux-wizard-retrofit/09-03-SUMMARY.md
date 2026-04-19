---
phase: 09-opt-in-ux-wizard-retrofit
plan: "03"
subsystem: ui
tags: [avalonia, mvvm, windroseplus, retrofit, dashboard, opt-in]

# Dependency graph
requires:
  - phase: 09-01
    provides: OptInState enum, AppSettings extensions (WindrosePlusOptInStateByServer/ActiveByServer), RconPasswordGenerator, FreePortProbe, SteamIdParser, migration logic
  - phase: 09-02
    provides: IWindrosePlusOptInContext interface, WindrosePlusOptInControl UserControl, all Retrofit.* bilingual string keys, IWindrosePlusService.InstallAsync

provides:
  - RetrofitBannerViewModel — non-modal banner state + commands (OpenRetrofitDialogCommand, DismissOptOutCommand), implements IWindrosePlusOptInContext
  - RetrofitDialog — modal Avalonia Window hosting WindrosePlusOptInControl, centered over main window, Escape/Enter key handling
  - DashboardViewModel — integrated RetrofitBannerVisible + RetrofitBanner child observable, re-evaluated on every refresh cycle; StateChanged handler wires banner dismissal back into RefreshAsync
  - DashboardView — retrofit banner Border (above existing cards), binds all 4 Retrofit.Banner.* string keys, wires both CTAs
  - WindrosePlusCard in SettingsPage — install/manage WindrosePlus from Settings regardless of current server OptInState (extra, unplanned)

affects:
  - Phase 10 (Health) — DashboardViewModel refresh pattern is the integration point for health-check banners
  - Phase 12 (Empty States) — RetrofitBannerViewModel.OpenRetrofitDialogCommand is the CTA target for empty-state flows

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Child-ViewModel-as-Banner: DashboardViewModel holds a nullable RetrofitBannerViewModel child; visibility is computed on each timer tick; StateChanged event triggers a RefreshAsync call back up"
    - "One-shot dialog pattern: RetrofitDialog closes with bool result; caller (RetrofitBannerViewModel.OpenRetrofitDialogAsync) runs install only on true; cancel leaves state=NeverAsked"
    - "Factory-new pattern: RetrofitBannerViewModel is constructed via `new` inside DashboardViewModel using its own injected deps (IWindrosePlusService, IAppSettingsService, IToastService) — avoids a DI-registered type that requires a runtime string arg"

key-files:
  created:
    - src/WindroseServerManager.App/ViewModels/RetrofitBannerViewModel.cs
    - src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml
    - src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml.cs
  modified:
    - src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/DashboardView.axaml
    - src/WindroseServerManager.App/Views/Pages/SettingsPage.axaml (WindrosePlus card, extra)
    - src/WindroseServerManager.App/ViewModels/SettingsViewModel.cs (WindrosePlus card VM, extra)

key-decisions:
  - "RetrofitBannerViewModel is NOT registered in DI — it takes a per-server string arg (serverInstallDir) at construction time; DashboardViewModel creates it via `new` and passes its own injected services"
  - "StateChanged event on RetrofitBannerViewModel triggers DashboardViewModel.RefreshAsync on dismiss/install-complete — avoids a timer-lag between user action and banner disappearance"
  - "RetrofitBannerViewModel.OpenRetrofitDialogAsync: dialog cancel leaves OptInState=NeverAsked (banner reappears next refresh); only true result from dialog proceeds to InstallAsync — no silent side-effects on cancel"
  - "No close-X on the banner (RETRO-03 compliance): the only exits are explicit 'Jetzt installieren' or 'Nicht jetzt' — no dismissal without a persisted decision"
  - "WindrosePlus settings card added in SettingsPage (unplanned deviation) — enables managing WindrosePlus state (install/disable) from a stable location independently of the dashboard banner lifecycle"

patterns-established:
  - "Banner-via-ChildVM: nullable child ViewModel as banner state, parent computes visibility in refresh cycle, StateChanged propagates back up"
  - "Dialog-returns-bool: Avalonia ShowDialog<bool> gate — no business logic runs until dialog confirms"

requirements-completed:
  - RETRO-02
  - RETRO-03

# Metrics
duration: "~2h (across 2 tasks + smoke-test checkpoint)"
completed: "2026-04-19"
---

# Phase 9 Plan 03: Retrofit Banner + Dialog Summary

**Non-modal DashboardView retrofit banner + RetrofitDialog reusing WindrosePlusOptInControl — RETRO-02/03 satisfied, all choices persist, no silent installs**

## Performance

- **Duration:** ~2h (Tasks 1-2 + smoke-test checkpoint)
- **Started:** 2026-04-19
- **Completed:** 2026-04-19
- **Tasks:** 3 (2 auto + 1 human smoke-test checkpoint)
- **Files modified:** 7

## Accomplishments

- RetrofitBannerViewModel created — implements IWindrosePlusOptInContext so it wires directly into WindrosePlusOptInControl without duplication; handles opt-out persistence and InstallAsync orchestration
- RetrofitDialog created — Avalonia Window hosting WindrosePlusOptInControl; cancel leaves NeverAsked state (no silent persist), confirm triggers InstallAsync; Escape key closes with false
- DashboardViewModel integrated — RetrofitBannerVisible computed on every refresh tick from AppSettings; banner auto-hides while IsInstalling (Pitfall 7 mitigated); StateChanged handler detaches on dispose to prevent leaks
- DashboardView banner rendered above crash-warning card — no close-X, two explicit CTAs only
- WindrosePlus settings card added to SettingsPage (extra, see Deviations) — allows managing WP state from Settings independently of dashboard banner

## Task Commits

1. **Task 1: RetrofitBannerViewModel + RetrofitDialog + DI** - `5e0275f` (feat)
2. **Task 2: DashboardViewModel + DashboardView banner integration + Settings WP card** - `54f3256` + `95e9cdd` (feat)
3. **Task 3: Smoke test** — APPROVED (checkpoint, no commit)

## Files Created/Modified

- `src/WindroseServerManager.App/ViewModels/RetrofitBannerViewModel.cs` — Banner state + OpenRetrofitDialogCommand + DismissOptOutCommand; implements IWindrosePlusOptInContext
- `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml` — Modal Window, Width=540, SizeToContent=Height, hosts WindrosePlusOptInControl + progress + error + footer buttons
- `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml.cs` — Escape=Close(false), Cancel=Close(false), Confirm=Close(true) if CanConfirmInstall
- `src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs` — Added IWindrosePlusService dep, RetrofitBannerVisible + RetrofitBanner observables, banner computation in RefreshAsync, OnRetrofitStateChanged handler
- `src/WindroseServerManager.App/Views/Pages/DashboardView.axaml` — Retrofit banner Border above crash-warning card; all 4 Retrofit.Banner.* keys; OpenRetrofitDialogCommand + DismissOptOutCommand bindings
- `src/WindroseServerManager.App/Views/Pages/SettingsPage.axaml` — WindrosePlus management card (install/manage from Settings)
- `src/WindroseServerManager.App/ViewModels/SettingsViewModel.cs` — WindrosePlus card VM wiring

## Decisions Made

- **RetrofitBannerViewModel not in DI** — takes `string serverInstallDir` at construction; DashboardViewModel uses `new` with its own injected deps. Registering a factory in DI was ruled out as unnecessary overhead for a single-use type.
- **StateChanged event** — synchronous C# event chosen over MediatR notification for the DashboardViewModel→RefreshAsync feedback loop. The banner lifecycle is local to the dashboard; there are no other subscribers, making MediatR overkill.
- **Retrofit refresh method** — `RefreshAsync` is the actual method name in DashboardViewModel (confirmed by reading the full file); `OnRetrofitStateChanged` calls `_ = RefreshAsync(CancellationToken.None)` (fire-and-forget with try-catch inside RefreshAsync).
- **No DI registration for RetrofitBannerViewModel** — documented in plan as "skip step 4"; confirmed correct.

## Deviations from Plan

### Auto-added Extra Work

**1. [Rule 2 - Missing Critical] WindrosePlus Settings Card**
- **Found during:** Task 2 (DashboardView integration)
- **Issue:** Banner-only surface means users who dismissed "Nicht jetzt" have no way to install WindrosePlus later without editing JSON manually. A persistent install/manage entry in Settings is required for correct UX.
- **Fix:** Added WindrosePlusCard section to SettingsPage.axaml and wired SettingsViewModel accordingly. Card shows current OptInState and offers Install/Disable actions.
- **Files modified:** `Views/Pages/SettingsPage.axaml`, `ViewModels/SettingsViewModel.cs`
- **Committed in:** `95e9cdd`

---

**Total deviations:** 1 auto-added (Rule 2 — missing critical UX path)
**Impact on plan:** Strictly additive. Required by the "no silent upgrades, no impossible re-enable" invariant. No scope creep beyond the Settings card.

## Issues Encountered

None — build green after both tasks. Smoke test approved by user.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Phase 12 (Empty States):** RetrofitBannerViewModel.OpenRetrofitDialogCommand is the exact CTA target for in-view install flows. DashboardViewModel.RefreshAsync is the integration point for triggering banner re-evaluation after Phase 12 CTAs fire.
- **Phase 10 (Health):** DashboardViewModel's refresh-tick pattern (2s timer → RefreshAsync) is also the correct integration point for health-check banner visibility.
- Phase 9 is now fully complete (3/3 plans). All 7 requirements (WIZARD-01..04, RETRO-01..03) satisfied.

## Self-Check

**Created files:**
- `src/WindroseServerManager.App/ViewModels/RetrofitBannerViewModel.cs` — confirmed (commit `5e0275f`)
- `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml` — confirmed (commit `5e0275f`)
- `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml.cs` — confirmed (commit `5e0275f`)

**Commits present:**
- `5e0275f` — Task 1: RetrofitBannerViewModel + RetrofitDialog + DI
- `54f3256` — Task 2: DashboardViewModel + DashboardView banner integration
- `95e9cdd` — Task 2.1 extra: WindrosePlus Settings card

## Self-Check: PASSED

---
*Phase: 09-opt-in-ux-wizard-retrofit*
*Completed: 2026-04-19*
