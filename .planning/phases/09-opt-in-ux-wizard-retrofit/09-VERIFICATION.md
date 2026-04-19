---
phase: 09-opt-in-ux-wizard-retrofit
verified: 2026-04-19T00:00:00Z
status: passed
score: 9/9 must-haves verified
gaps: []
human_verification:
  - test: "3-step wizard UI smoke — stepper navigation, feature grid, eye-toggle, copy, regenerate, SteamID validation, language switch"
    expected: "All 7 smoke-test points from plan 09-02 Task 3 pass: centered modal, feature tiles visible, opt-in toggle hides/shows secrets form, vanity URL shows error border, EN language flip works"
    why_human: "Visual rendering, password clipboard copy, eye-toggle PasswordChar swap, and Avalonia window centering are not verifiable by grep or build"
  - test: "Retrofit banner lifecycle — NeverAsked seed triggers banner, Nicht jetzt persists OptedOut, cancel leaves NeverAsked"
    expected: "All 8 smoke-test points from plan 09-03 Task 3 pass: banner appears for test server, hides after dismiss, JSON shows OptedOut, cancel does not write JSON"
    why_human: "Real runtime behaviour (timer-driven refresh, AppSettings JSON read-back, banner visibility on page switch) requires manual execution"
---

# Phase 9: Opt-In UX — Wizard + Retrofit — Verification Report

**Phase Goal:** Ship explicit opt-in UX for WindrosePlus — new-server wizard (3-step modal) and retrofit banner for existing servers — so users can make an informed choice about WindrosePlus without silent upgrades.
**Verified:** 2026-04-19
**Status:** passed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | AppSettings persists OptInState, RCON password, dashboard port, admin SteamID per server | VERIFIED | All 4 dicts present in AppSettings.cs lines 68-74; ServerInstallInfo read-through params lines 11-14 |
| 2 | RconPasswordGenerator returns URL-safe string of at least 24 chars from [A-Za-z0-9-_] | VERIFIED | RconPasswordGenerator.cs uses RandomNumberGenerator.GetBytes + UrlSafeAlphabet; 5 unit tests green |
| 3 | SteamIdParser accepts raw SteamID64 and numeric /profiles/ URL, rejects vanity /id/ and garbage | VERIFIED | SteamIdParser.cs has 3 compiled regexes with 5s timeout; 14-case theory test suite green (extended during plan 02 to cover vanity path) |
| 4 | FreePortProbe returns port in 18080..18099 when free, falls back to OS ephemeral | VERIFIED | FreePortProbe.cs uses TcpListener loop + IPAddress.Loopback fallback; 3 tests including exhaustion test green |
| 5 | On first v1.2 load, servers in WindrosePlusActiveByServer without an opt-in entry are seeded to NeverAsked; existing entries not overwritten | VERIFIED | AppSettingsService.cs MigrateToV12 at lines 69, 90, 149-155 (definition + 2 call sites); ContainsKey guard present; 7 migration tests green |
| 6 | New-server wizard (3-step modal) opens from InstallationView with feature grid, opt-in toggle, RCON/port/SteamID form, and persists all 4 fields on install | VERIFIED | InstallWizardWindow.axaml has stepper + 3 step panels; WindrosePlusOptInControl.axaml has feature grid + secrets form; InstallWizardViewModel writes all 4 AppSettings dicts; wired via OpenNewServerWizardCommand on InstallationView.axaml:22 |
| 7 | DE + EN bilingual strings for Wizard.* / Feature.* / Error.WindrosePlus.* / Retrofit.* exist with identical keyspace | VERIFIED | 20 Wizard.* keys + 7 Feature.* keys in both Strings.de.axaml and Strings.en.axaml; all 5 Retrofit.Banner/Dialog keys present in both files |
| 8 | Retrofit banner on DashboardView shows when OptInState=NeverAsked and WindrosePlusActive=false; Nicht jetzt sets OptedOut permanently; install runs WindrosePlus only (no SteamCMD) | VERIFIED | DashboardViewModel.cs computes RetrofitBannerVisible in RefreshAsync (3 occurrences); RetrofitBannerViewModel has DismissOptOutCommand writing OptedOut; no IServerInstallService reference in RetrofitBannerViewModel.cs |
| 9 | Retrofit and wizard both reuse WindrosePlusOptInControl via IWindrosePlusOptInContext | VERIFIED | Both InstallWizardViewModel and RetrofitBannerViewModel implement IWindrosePlusOptInContext; RetrofitDialog.axaml:24 hosts WindrosePlusOptInControl |

**Score:** 9/9 truths verified

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|---------|---------|--------|---------|
| `src/WindroseServerManager.Core/Models/OptInState.cs` | enum OptInState { NeverAsked, OptedIn, OptedOut } with JsonStringEnumConverter | VERIFIED | File exists, correct enum values and attribute |
| `src/WindroseServerManager.Core/Models/AppSettings.cs` | 4 new per-server dicts | VERIFIED | Lines 68-74 confirmed |
| `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` | 4 read-through record properties | VERIFIED | Lines 11-14 confirmed |
| `src/WindroseServerManager.Core/Services/RconPasswordGenerator.cs` | Generate() using RandomNumberGenerator | VERIFIED | Exists, substantive (24-char URL-safe impl) |
| `src/WindroseServerManager.Core/Services/SteamIdParser.cs` | ExtractSteamId64 with regex + 5s timeout | VERIFIED | 3 compiled regexes, all with TimeSpan.FromSeconds(5) |
| `src/WindroseServerManager.Core/Services/FreePortProbe.cs` | FindFreePort with range + OS fallback | VERIFIED | IPAddress.Loopback loop + fallback on port 0 |
| `src/WindroseServerManager.Core/Services/AppSettingsService.cs` | MigrateToV12 called from LoadAsync | VERIFIED | 2 call sites + private method definition |
| `tests/.../Phase9/` (5 test files) | Phase9-tagged unit tests | VERIFIED | 5 files present; 35 tests, all green |
| `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml` | Feature grid + secrets form UserControl | VERIFIED | File exists; Feature.Players binding + Wizard.WindrosePlus.OptIn + Error.WindrosePlus.SteamIdInvalid confirmed |
| `src/WindroseServerManager.App/Views/Controls/WindrosePlusOptInControl.axaml.cs` | Event handlers for eye-toggle / copy / regenerate | VERIFIED | partial class WindrosePlusOptInControl : UserControl |
| `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml` | 3-step wizard window, 560px, CenterOwner | VERIFIED | Wizard.Title + 3 step circles + IntEquals converter step panels |
| `src/WindroseServerManager.App/Views/Dialogs/InstallWizardWindow.axaml.cs` | partial class InstallWizardWindow : Window | VERIFIED | Confirmed |
| `src/WindroseServerManager.App/ViewModels/InstallWizardViewModel.cs` | State machine + orchestration, implements IWindrosePlusOptInContext | VERIFIED | RconPasswordGenerator, FreePortProbe, SteamIdParser, _wplus.InstallAsync all called; WindrosePlusOptInStateByServer written |
| `src/WindroseServerManager.App/ViewModels/IWindrosePlusOptInContext.cs` | Shared interface for UserControl DataContext | VERIFIED | File exists; both VMs implement it |
| `src/WindroseServerManager.App/ViewModels/RetrofitBannerViewModel.cs` | Banner state + commands, implements IWindrosePlusOptInContext, no SteamCMD | VERIFIED | DismissOptOutCommand writes OptedOut; _wplus.InstallAsync present; no IServerInstallService reference |
| `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml` | Modal dialog hosting WindrosePlusOptInControl | VERIFIED | controls:WindrosePlusOptInControl at line 24 |
| `src/WindroseServerManager.App/Views/Dialogs/RetrofitDialog.axaml.cs` | partial class RetrofitDialog : Window | VERIFIED | Confirmed |
| `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` | All Wizard.* / Feature.* / Retrofit.* keys | VERIFIED | 20 Wizard.* + 7 Feature.* + 5 Retrofit.* + Installation.NewServer |
| `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` | Identical keyspace to DE | VERIFIED | 20 Wizard.* + 7 Feature.* + 5 Retrofit.* + Installation.NewServer |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| AppSettingsService.cs | AppSettings.cs | MigrateToV12 seeds WindrosePlusOptInStateByServer[key] = NeverAsked | WIRED | ContainsKey guard + assignment confirmed at lines 154-155 |
| ServerInstallInfo.cs | AppSettings.cs | Record positional params mirror 4 new dicts | WIRED | WindrosePlusRconPassword/DashboardPort/AdminSteamId/OptInState params confirmed |
| InstallationView.axaml | InstallWizardWindow.axaml | OpenNewServerWizardCommand on button → new InstallWizardWindow in VM | WIRED | InstallationView.axaml:22 + InstallationViewModel.cs:210 |
| InstallWizardViewModel.cs | IWindrosePlusService | _wplus.InstallAsync called after SteamCMD completes | WIRED | Line 157 in InstallWizardViewModel.cs |
| InstallWizardViewModel.cs | AppSettingsService | UpdateAsync writes WindrosePlusOptInStateByServer[InstallDir] | WIRED | Line 166 confirmed |
| App.axaml.cs | InstallWizardViewModel.cs | services.AddTransient<InstallWizardViewModel>() | WIRED | App.axaml.cs line 101 |
| DashboardViewModel.cs | RetrofitBannerViewModel.cs | RetrofitBanner child ViewModel, recomputed in RefreshAsync | WIRED | new RetrofitBannerViewModel + RetrofitBannerVisible computed in refresh; 3 occurrences |
| DashboardView.axaml | DashboardViewModel.cs | IsVisible bound to RetrofitBannerVisible; CTAs bound to RetrofitBanner.* commands | WIRED | All 4 Retrofit.Banner.* keys + OpenRetrofitDialogCommand + DismissOptOutCommand confirmed |
| RetrofitBannerViewModel.cs | IWindrosePlusService | InstallAsync called from RunInstallAsync (retrofit-only, no SteamCMD) | WIRED | Line 135; no IServerInstallService present |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|------------|------------|-------------|--------|---------|
| WIZARD-01 | 09-02 | Wizard step 2 shows WindrosePlus feature list + GitHub link | SATISFIED | WindrosePlusOptInControl.axaml has Feature.Players/KickBan/Broadcast/Events/SeaChart/IniEditor tiles + GitHub link button |
| WIZARD-02 | 09-02 | WindrosePlus default-on; one-click opt-out | SATISFIED | IsOptingIn=true default in InstallWizardViewModel; CheckBox toggles secrets form visibility |
| WIZARD-03 | 09-01 | Wizard sets secure random RCON password and captures admin SteamID | SATISFIED | RconPasswordGenerator.Generate(24) in constructor; SteamIdParser.ExtractSteamId64 validates on confirm |
| WIZARD-04 | 09-01 | Wizard picks free local port for dashboard | SATISFIED | FreePortProbe.FindFreePort() in constructor; stored to WindrosePlusDashboardPortByServer |
| RETRO-01 | 09-01 | First v1.2 launch detects per-server WindrosePlus install state | SATISFIED | MigrateToV12 seeds NeverAsked for every WindrosePlusActiveByServer key that lacks OptInState; runs in LoadAsync before Current exposed |
| RETRO-02 | 09-03 | Non-modal banner offers install with feature list + opt-out; choice persists | SATISFIED | DashboardView banner with OpenRetrofitDialogCommand + DismissOptOutCommand; DismissOptOutAsync writes OptedOut to AppSettings |
| RETRO-03 | 09-03 | Retrofit never installs silently; user must explicitly confirm | SATISFIED | No SteamCMD in RetrofitBannerViewModel; banner has no close-X (grep confirmed no close-X button); dialog cancel returns false without persisting |

All 7 phase requirements SATISFIED. No orphaned requirements.

---

## Anti-Patterns Found

No blockers or warnings found during scan of Phase 9 key files. The one pre-existing warning (`CS0067` unused events in `ModServiceTests.cs`) is outside Phase 9 scope and was present before this phase.

---

## Human Verification Required

### 1. Wizard UI Smoke Test

**Test:** Run `dotnet run --project src/WindroseServerManager.App`, navigate to Installation page, click "Neuen Server einrichten", walk through all 7 smoke-test points from plan 09-02 Task 3 (stepper navigation, feature grid, eye-toggle/copy/regenerate, SteamID vanity rejection, language switch to EN).
**Expected:** All 7 points pass — centered modal, 6 feature tiles, secrets form toggles with opt-in checkbox, vanity URL shows red border, EN strings flip correctly.
**Why human:** Visual rendering, clipboard operations, PasswordChar toggling, and Avalonia window-centering behaviour cannot be verified programmatically.

### 2. Retrofit Banner Lifecycle

**Test:** Manually seed a test server entry in the settings JSON with `WindrosePlusActiveByServer=false` and no OptInState entry. Start app, verify banner appears on Dashboard. Test path A ("Nicht jetzt") and verify JSON shows OptedOut after restart. Reset to NeverAsked, test path B (open dialog, cancel — confirm NeverAsked persists in JSON).
**Expected:** All 8 smoke-test points from plan 09-03 Task 3 pass. No close-X on banner. Dialog cancel does not write to JSON.
**Why human:** Timer-driven banner refresh, JSON read-back after restart, and no-close-X visual inspection require manual execution.

Note: Both human smoke tests were reportedly approved by the user during plan execution (Task 3 checkpoints in plans 09-02 and 09-03). These items are flagged here for completeness — re-run only if a regression is suspected.

---

## Gaps Summary

No gaps. All 9 observable truths are verified, all 19 required artifacts exist and are substantive and wired, all 9 key links are confirmed, and all 7 requirements (WIZARD-01..04, RETRO-01..03) are satisfied by actual codebase evidence.

The only open items are the two human verification entries, which were already approved during plan execution smoke tests and are noted here for traceability only.

---

_Verified: 2026-04-19_
_Verifier: Claude (gsd-verifier)_
