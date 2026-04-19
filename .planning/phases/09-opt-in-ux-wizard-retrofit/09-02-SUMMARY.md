---
phase: 09-opt-in-ux-wizard-retrofit
plan: 02
subsystem: ui
tags: [avalonia, wizard, windroseplus, opt-in, mvvm, bilingual, steam-id, rcon, port-probe]

# Dependency graph
requires:
  - phase: 09-opt-in-ux-wizard-retrofit/09-01
    provides: OptInState enum; 4 per-server AppSettings dicts; RconPasswordGenerator / SteamIdParser / FreePortProbe helper statics; AppSettingsService.UpdateAsync

provides:
  - WindrosePlusOptInControl UserControl (feature grid 3x2, GitHub link, opt-in toggle, RCON/port/SteamID secrets form) — reused verbatim by Plan 03 retrofit dialog
  - IWindrosePlusOptInContext interface — implemented by InstallWizardViewModel; Plan 03's RetrofitDialogViewModel will implement it too without touching the UserControl
  - InstallWizardWindow (3-step Avalonia Window, 560px wide, CenterOwner, SizeToContent=Height)
  - InstallWizardViewModel (CommunityToolkit.Mvvm, implements IWindrosePlusOptInContext, orchestrates SteamCMD → WindrosePlus install, persists all 4 per-server fields via UpdateAsync)
  - "Neuen Server einrichten" entry point button wired on InstallationView via OpenNewServerWizardCommand
  - ~30 bilingual string keys: Wizard.* / Feature.* / Error.WindrosePlus.* / Retrofit.* / Installation.NewServer (both DE + EN, identical keyspace)
affects: [09-03-retrofit, 10-health, 11-feature-views, 12-empty-states]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - IWindrosePlusOptInContext interface decouples UserControl from concrete VM type — code-behind casts DataContext to the interface, not to InstallWizardViewModel
    - Avalonia TextBox with PasswordChar="•" (no PasswordBox in Avalonia) for RCON field; FindControl<TextBox>("RconPasswordBox") for toggle (x:Name field access unavailable for new controls)
    - Window-level keyboard handling (Escape → cancel, Enter → advance) via KeyDown override in code-behind
    - StorageProvider.OpenFolderPickerAsync for directory browse (consistent with existing InstallationViewModel)
    - Transient DI registration for InstallWizardViewModel — each wizard open gets fresh password + port probe

key-files:
  created:
    - src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml
    - src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml.cs
    - src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml
    - src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml.cs
    - src/WindroseServerManager.App/ViewModels/InstallWizardViewModel.cs
    - src/WindroseServerManager.App/ViewModels/IWindrosePlusOptInContext.cs
  modified:
    - src/WindroseServerManager.App/ViewModels/InstallationViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/InstallationView.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
    - src/WindroseServerManager.App/App.axaml.cs

key-decisions:
  - "IWindrosePlusOptInContext defined as a separate interface file — both InstallWizardViewModel (this plan) and RetrofitDialogViewModel (Plan 03) implement it; the UserControl's code-behind casts to the interface only, zero coupling to either VM type"
  - "Stepper rendered via three inline named Borders with Classes toggled from code-behind on CurrentStep change — simpler and more maintainable than an IntEqualsConverter + ItemsControl approach for a fixed 3-step flow"
  - "Feature grid uses a compact 3x2 Grid layout (3 columns x 2 rows) with inline port row — avoids vertical scroll on step 2 at 560px width"
  - "SteamIdParser extended to accept vanity URLs (steamcommunity.com/id/name) by routing them through the existing error path with a dedicated error caption — plan originally only listed numeric /profiles/ URLs"
  - "Retrofit.* string keys ship in this plan (Wizard step 2 and strings files) so Plan 03 can reuse them without adding new keys"
  - "Fluent icon glyphs on RCON password buttons set via explicit FontFamily='{StaticResource SymbolThemeFontFamily}' — default FontFamily on Button children in Avalonia does not inherit from NavigationView context"

patterns-established:
  - "IWindrosePlusOptInContext: define a minimal interface for a UserControl's DataContext; code-behind never imports the concrete VM"
  - "Wizard-style dialog: SizeToContent=Height + CanResize=False + CenterOwner; content panels toggled via IsVisible on CurrentStep"
  - "Transient ViewModel for multi-instance dialogs — each ShowDialog call resolves a fresh Transient from DI"

requirements-completed: [WIZARD-01, WIZARD-02]

# Metrics
duration: ~45min
completed: 2026-04-19
---

# Phase 9 Plan 02: Wizard UI Summary

**3-step install wizard (WindrosePlusOptInControl + InstallWizardWindow + InstallWizardViewModel) with shared IWindrosePlusOptInContext interface, feature grid opt-in toggle, RCON/port/SteamID secrets form, and bilingual string coverage for all Wizard.* / Feature.* / Retrofit.* keys — manually smoke-tested and approved.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-04-19T18:10:18Z
- **Completed:** 2026-04-19
- **Tasks:** 2 auto + 1 checkpoint (3 total)
- **Files created:** 6
- **Files modified:** 5

## Accomplishments

- Delivered the primary v1.2 opt-in surface: every new-server flow passes through the 3-step wizard that presents the WindrosePlus feature list, collects RCON password / dashboard port / admin Steam-ID, and writes all four per-server fields to AppSettings on install confirmation.
- Shipped `WindrosePlusOptInControl` as a reusable UserControl (feature grid + secrets form) backed by `IWindrosePlusOptInContext` — Plan 03 can embed it in the retrofit dialog without any changes to the control itself.
- Full bilingual coverage: ~35 new string keys across `Wizard.*`, `Feature.*`, `Error.WindrosePlus.*`, `Retrofit.*`, and `Installation.NewServer` — both `Strings.de.axaml` and `Strings.en.axaml` have identical keyspace, verified in the smoke test (language switch).

## Task Commits

1. **Task 1: WindrosePlusOptInControl + InstallWizardWindow + InstallWizardViewModel + DI + strings** — `a05fdb1` (feat)
2. **Task 2: Wire "Neuen Server einrichten" button on InstallationView** — `7a011d3` (feat)
   - Fix 2.1: Fluent icon glyphs on RCON password buttons — `032d170` (fix)
   - Fix 2.2: Wizard stays centered across steps — `4472038` (fix)
   - Fix 2.3: Steam-ID required-field UX (explain why Weiter is disabled) — `684cbff` (fix)
   - Fix 2.4: Accept vanity URLs in SteamIdParser + update tests — `a663ed0` (fix)
   - Fix 2.5: Compact feature grid 3×2 + inline port row — `296a905` (fix)
3. **Task 3: Human smoke test** — APPROVED (no code changes)

**Plan metadata:** TBD after this commit

## Files Created/Modified

### Created
- `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml` — Feature grid (3×2), GitHub link, disclaimer, opt-in toggle, RCON/port/SteamID secrets form
- `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml.cs` — Eye-toggle / copy / regenerate event handlers; DataContext cast to IWindrosePlusOptInContext
- `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml` — 560px, SizeToContent, 3-step stepper header, step content panels, footer buttons
- `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml.cs` — OnBrowseClick (StorageProvider), Escape/Enter keyboard routing, CloseRequested subscription
- `src/WindroseServerManager.App/ViewModels/InstallWizardViewModel.cs` — State machine (CurrentStep 1→3), GoNext/GoBack/Install commands, CanGoNext validation gates, IWindrosePlusOptInContext impl
- `src/WindroseServerManager.App/ViewModels/IWindrosePlusOptInContext.cs` — Minimal interface (IsOptingIn, RconPassword, DashboardPort, AdminSteamId, HasSteamIdError, RegeneratePassword, ValidateSteamId)

### Modified
- `src/WindroseServerManager.App/ViewModels/InstallationViewModel.cs` — +OpenNewServerWizardCommand; resolves Transient VM from DI, wires CloseRequested, calls ShowDialog, refreshes install info on close
- `src/WindroseServerManager.App/Views/Pages/InstallationView.axaml` — "Neuen Server einrichten" primary button added before existing Install/Update button
- `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` — +~35 keys: Wizard.*, Feature.*, Error.WindrosePlus.*, Retrofit.*, Installation.NewServer
- `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` — Identical keyspace, EN values
- `src/WindroseServerManager.App/App.axaml.cs` — +`s.AddTransient<InstallWizardViewModel>()`

## IWindrosePlusOptInContext Interface Shape (Critical for Plan 03)

Plan 03's `RetrofitDialogViewModel` MUST implement this interface to use `WindrosePlusOptInControl` without changes:

```csharp
public interface IWindrosePlusOptInContext
{
    bool IsOptingIn { get; set; }
    string RconPassword { get; set; }
    int DashboardPort { get; }
    string AdminSteamId { get; set; }
    bool HasSteamIdError { get; }
    void RegeneratePassword();
    void ValidateSteamId();
}
```

## Retrofit.* Keys Shipped in This Plan (Plan 03 Reuses Without Adding New Ones)

The following keys already exist in both `Strings.de.axaml` and `Strings.en.axaml`:

| Key | DE | EN |
|-----|----|----|
| `Retrofit.Banner.Title` | WindrosePlus ist verfügbar | WindrosePlus is available |
| `Retrofit.Banner.Body` | Erweitere deinen Server mit Spieler-Management, Ereignis-Log und mehr. | Enhance your server with player management, event log and more. |
| `Retrofit.Banner.Action.Install` | Installieren | Install |
| `Retrofit.Banner.Action.Later` | Später | Later |
| `Retrofit.Dialog.Title` | WindrosePlus einrichten | Set Up WindrosePlus |

Plan 03 can reference all five keys directly without editing the Strings files.

## Decisions Made

1. **IWindrosePlusOptInContext as a separate interface file** — avoids the UserControl code-behind importing either VM assembly; keeps the shared UserControl independently testable.
2. **Compact 3×2 feature grid** — the original plan spec described a 2×3 grid (2 cols × 3 rows); the compact 3-column layout eliminates vertical scroll inside the 560 px dialog at normal DPI. Smoke-test confirmed no scroll needed.
3. **SteamIdParser extended to handle vanity URLs** — the plan spec said "accepts raw SteamID64 and numeric /profiles/ URL; rejects vanity URLs". During smoke testing it emerged that the *error path* for vanity URLs still needed to surface a clear inline caption distinguishing "vanity URL (not supported)" from "completely invalid input". The parser itself was updated to recognize the vanity pattern and the error text reflects it.
4. **Fluent icon glyphs on icon buttons** — required an explicit `FontFamily='{StaticResource SymbolThemeFontFamily}'` on the three `TextBlock` glyph elements inside the RCON button group; Avalonia does not inherit the symbol font family from parent scope in this context.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fluent icon glyphs not rendering on RCON password buttons**
- **Found during:** Task 2 (smoke test pre-verification)
- **Issue:** Icon buttons (eye-toggle, copy, regenerate) showed empty squares instead of Fluent glyphs
- **Fix:** Added explicit `FontFamily='{StaticResource SymbolThemeFontFamily}'` on `TextBlock` glyph children
- **Files modified:** `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml`
- **Verification:** Smoke test confirmed icons render correctly
- **Committed in:** `032d170`

**2. [Rule 1 - Bug] Wizard dialog repositioned to screen corner on step change**
- **Found during:** Task 2 (smoke test step navigation)
- **Issue:** `WindowStartupLocation=CenterOwner` only applies at initial display; on step change the content height reflow caused the window to drift to the top-left
- **Fix:** Called `this.CenterOn(Owner)` inside the step-change handler to re-centre after each height change
- **Files modified:** `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml.cs`
- **Verification:** Wizard remains centered through all three steps in smoke test
- **Committed in:** `4472038`

**3. [Rule 2 - Missing Critical] Steam-ID empty field gave no explanation for disabled "Weiter" button**
- **Found during:** Task 2 (smoke test step 2 UX)
- **Issue:** With opt-in checked and Steam-ID field empty, "Weiter" was disabled but there was no hint explaining why — user could not know the field was required
- **Fix:** Added an inline required-field caption below the SteamID TextBox visible when `IsOptingIn && string.IsNullOrEmpty(AdminSteamId)`
- **Files modified:** `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml`
- **Verification:** Caption visible when field empty; disappears on valid input
- **Committed in:** `684cbff`

**4. [Rule 1 - Bug] SteamIdParser accepted vanity URLs silently**
- **Found during:** Task 2 (smoke test Steam-ID validation)
- **Issue:** Entering `https://steamcommunity.com/id/somevanity` did not produce the expected error border (parser returned null but error caption was generic; smoke tester observed no red border on some builds)
- **Fix:** Extended `SteamIdParser` to explicitly recognize the vanity `/id/` pattern and ensure the validation path returns null with a deterministic code path; updated unit tests to cover this case
- **Files modified:** `src/WindroseServerManager.Core/Services/SteamIdParser.cs`, `tests/.../Phase9/SteamIdParserTests.cs`
- **Verification:** Smoke test: vanity URL → red border + error caption. Valid SteamID64 → no error.
- **Committed in:** `a663ed0`

**5. [Rule 2 - Missing Critical] Feature grid caused vertical scroll inside wizard step 2**
- **Found during:** Task 2 (smoke test step 2 layout)
- **Issue:** The original 2-column × 3-row grid plus the secrets form exceeded the visible area at 560 px width, requiring scroll
- **Fix:** Switched to 3-column × 2-row grid and moved the port row inline with the port label to reduce total height
- **Files modified:** `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml`
- **Verification:** Step 2 fits without scroll in smoke test at 100% DPI
- **Committed in:** `296a905`

---

**Total deviations:** 5 auto-fixed (2 bugs, 2 missing critical, 1 bug)
**Impact on plan:** All five fixes directly required by smoke-test findings. No scope creep — every fix targets wizard step 2 correctness and UX clarity.

## Issues Encountered

- None beyond the five auto-fixed deviations above. Build was green after each fix before proceeding to the next.

## User Setup Required

None — no external service configuration required. The wizard writes to `AppSettings` (local JSON); no environment variables or external credentials needed for Plan 02.

## Next Phase Readiness

**Ready for Plan 03 (retrofit dialog):**
- `WindrosePlusOptInControl` is a stable, tested UserControl. Plan 03 embeds it as-is.
- `IWindrosePlusOptInContext` interface is defined and documented — `RetrofitDialogViewModel` implements the same 7 members.
- All `Retrofit.*` string keys are already present in both language files; Plan 03 adds zero new string keys.
- `OptInState` persistence path (`UpdateAsync` writing `WindrosePlusOptInStateByServer`) is proven by `InstallWizardViewModel.InstallAsync`.

**No blockers.**

## Self-Check

Verification of key artifacts:
- `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml` FOUND
- `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml.cs` FOUND
- `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml` FOUND
- `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml.cs` FOUND
- `src/WindroseServerManager.App/ViewModels/InstallWizardViewModel.cs` FOUND
- `src/WindroseServerManager.App/ViewModels/IWindrosePlusOptInContext.cs` FOUND
- Commits `a05fdb1`, `7a011d3`, `032d170`, `4472038`, `684cbff`, `a663ed0`, `296a905` FOUND in git log

## Self-Check: PASSED

---
*Phase: 09-opt-in-ux-wizard-retrofit*
*Completed: 2026-04-19*
