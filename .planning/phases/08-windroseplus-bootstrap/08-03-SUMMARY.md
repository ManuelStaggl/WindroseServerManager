---
phase: 08-windroseplus-bootstrap
plan: 03
subsystem: infra
tags: [di, avalonia, about-dialog, launcher, localization, mit-license, windroseplus]

requires:
  - phase: 08-windroseplus-bootstrap
    provides: WindrosePlusService implementation (Plan 02) — ResolveLauncher, EnsureInstalled, GitHub/cache fetch
  - phase: 08-windroseplus-bootstrap
    provides: IWindrosePlusService contract + AppSettings per-server maps (Plan 01)
provides:
  - DI-registered IWindrosePlusService singleton available app-wide
  - ServerProcessService automatically switches between WindroseServer.exe and StartWindrosePlusServer.bat based on per-server WindrosePlusActive flag (WPLUS-04)
  - About dialog "Third-Party Licenses" section showing WindrosePlus MIT text + GitHub link (WPLUS-03)
  - 6 bilingual DE/EN strings reusable by Phases 9-12 (About.*, Warning.WindrosePlusBatMissing, Error.WindrosePlusOfflineFirstInstall)
affects: [09-wizard-retrofit, 10-health-support, 11-feature-views, 12-empty-states]

tech-stack:
  added: []
  patterns:
    - "IWindrosePlusService constructor-injected into ServerProcessService — launcher resolution is pure, no side effects in StartAsync"
    - "BuildInstallInfo(dir) reads AppSettings per-server maps → ServerInstallInfo → ResolveLauncher — single place to read opt-in state"
    - "Embedded LICENSE via AvaloniaResource + avares:// URI, loaded on demand via AssetLoader (lazy, never parsed until user clicks)"
    - "FindControl<T>(name) instead of x:Name field access for dynamically-added AXAML elements when generated fields are absent"

key-files:
  created:
    - src/WindroseServerManager.App/Resources/Licenses/WindrosePlus-LICENSE.txt
    - tests/WindroseServerManager.Core.Tests/Services/ServerProcessServiceLauncherTests.cs
  modified:
    - src/WindroseServerManager.App/App.axaml.cs (DI registration, line 75)
    - src/WindroseServerManager.Core/Services/ServerProcessService.cs (launcher indirection + helpers)
    - src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml (Third-Party Licenses row)
    - src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml.cs (OnShowWindrosePlusLicenseClick + FindControl pattern)
    - src/WindroseServerManager.App/WindroseServerManager.App.csproj (AvaloniaResource include)
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml

key-decisions:
  - "DI registration placed immediately after ISteamCmdService registration at App.axaml.cs line 75 — co-located with other Core service singletons"
  - "Strings files live under Resources/Strings/ (not Resources/) — plan path was outdated; kept existing project convention"
  - "Reused existing BrandNavySurfaceAltBrush + BrandNavyBorderBrush for license box — no new brush introduced"
  - "License box uses Consolas/Courier New monospace at 11px with MaxHeight=220 + ScrollViewer — MIT text stays readable without expanding the dialog"
  - "FindControl<TextBlock>/<Border> is used in OnShowWindrosePlusLicenseClick because Avalonia did not generate strong-typed fields for the new x:Name-tagged elements; matches pre-existing VersionText pattern in the same constructor"

patterns-established:
  - "Launcher resolution pattern: ServerProcessService calls IWindrosePlusService.ResolveLauncher(dir, info) — future server-lifecycle services (restart, health-check launch) should follow the same indirection"
  - "Bilingual string key naming: {Area}.{Context}.{Detail} (About.ThirdPartyLicenses.Heading, Warning.WindrosePlusBatMissing) — Phases 9-12 extend this namespace"
  - "Embedded third-party LICENSE pattern: Resources/Licenses/{Project}-LICENSE.txt + AvaloniaResource + avares:// — future bundled third-party code (e.g., UE4SS if rebundled) reuses this"

requirements-completed: [WPLUS-03, WPLUS-04]

duration: ~12min
completed: 2026-04-19
---

# Phase 8 Plan 03: WindrosePlus Wiring Summary

**DI-wired IWindrosePlusService singleton driving ServerProcessService launcher-switch and an About-dialog Third-Party Licenses section with embedded MIT text and bilingual strings.**

## Performance

- **Duration:** ~12 min (two feat commits + one post-checkpoint fix + docs)
- **Started:** 2026-04-19T18:04Z
- **Checkpoint approved:** 2026-04-19
- **Completed:** 2026-04-19
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files modified:** 7 (+ 2 created)

## Accomplishments

- IWindrosePlusService registered as a singleton in the Avalonia composition root (App.axaml.cs line 75) — resolves cleanly on app start
- ServerProcessService constructor now accepts IWindrosePlusService; StartAsync and ValidateCanStart both delegate launcher selection to ResolveLauncher(dir, info) — WPLUS-04 satisfied
- AppSettings per-server maps (WindrosePlusActiveByServer / WindrosePlusVersionByServer) bridged into ServerInstallInfo via new BuildInstallInfo(dir) helper — a single read-through point, no double-persistence
- About dialog gains a "Lizenzen von Drittanbietern" / "Third-Party Licenses" section with "WindrosePlus auf GitHub" button (opens https://github.com/HumanGenome/WindrosePlus), collapsible "Lizenztext anzeigen" button showing full MIT text — WPLUS-03 satisfied, product name "WindrosePlus" appears verbatim 3+ times
- 6 bilingual DE/EN strings added that Phases 9-12 can consume (Warning.WindrosePlusBatMissing + Error.WindrosePlusOfflineFirstInstall are already wired up in WindrosePlusService logging via key names, ready for toast use)
- 3 new ServerProcessServiceLauncherTests (active+bat, opt-out, fallback) — 71 passed / 0 failed in Core.Tests after integration

## Task Commits

1. **Task 1: DI registration + ServerProcessService launcher indirection + integration tests** — `4d5eddd` (feat)
2. **Task 2: Embed LICENSE + About dialog Third-Party Licenses section + localization** — `e1f1d39` (feat)
3. **Task 3: Human smoke test — About dialog and launcher switch** — approved 2026-04-19 (verification checkpoint, no code commit)
3a. **Post-checkpoint NRE fix** — `fb5ac48` (fix) — FindControl for WindrosePlus license elements

**Plan metadata:** (this commit)

## Files Created/Modified

- `src/WindroseServerManager.App/Resources/Licenses/WindrosePlus-LICENSE.txt` — Upstream HumanGenome MIT text, embedded as AvaloniaResource
- `tests/WindroseServerManager.Core.Tests/Services/ServerProcessServiceLauncherTests.cs` — 3 launcher-resolution integration [Fact]s
- `src/WindroseServerManager.App/App.axaml.cs` — `s.AddSingleton<IWindrosePlusService, WindrosePlusService>();` at line 75
- `src/WindroseServerManager.Core/Services/ServerProcessService.cs` — Constructor adds IWindrosePlusService; StartAsync + ValidateCanStart call ResolveLauncher; new BuildInstallInfo + CombineArgs helpers
- `src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml` — RowDefinitions grown to 4; new Grid.Row="2" StackPanel with section header, intro, GitHub button, license-toggle button, collapsible license Border
- `src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml.cs` — OnShowWindrosePlusLicenseClick reads avares:// URI via Avalonia.Platform.AssetLoader; uses FindControl<TextBlock>/<Border> for robustness against missing generated fields
- `src/WindroseServerManager.App/WindroseServerManager.App.csproj` — `<AvaloniaResource Include="Resources\Licenses\WindrosePlus-LICENSE.txt" />`
- `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` — 6 new German keys
- `src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml` — 6 new English keys

## Decisions Made

- **DI location:** directly below ISteamCmdService to keep Core-services block cohesive (App.axaml.cs line 75). No new region/comment needed.
- **Strings path:** plan said `Resources/Strings.de.axaml` but the project uses `Resources/Strings/Strings.de.axaml`. Kept existing convention.
- **Brushes:** BrandNavySurfaceAltBrush + BrandNavyBorderBrush already existed — no new brush defined. License box matches existing dialog surface hierarchy.
- **License toggle UX:** first click loads + shows, subsequent clicks toggle visibility without re-reading — reduces file I/O on the UI thread.
- **FindControl over x:Name fields:** Avalonia's source generator did not emit strong-typed fields for the newly added `WindrosePlusLicenseText` / `WindrosePlusLicenseBox` elements (reason undetermined — likely partial-class regeneration edge case). The same class already uses FindControl for `VersionText` in the constructor, so applying the pattern consistently was the simplest and most robust fix.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] NullReferenceException in OnShowWindrosePlusLicenseClick**
- **Found during:** Task 3 (human smoke test)
- **Issue:** Clicking "Lizenztext anzeigen" threw NRE — `WindrosePlusLicenseText` and `WindrosePlusLicenseBox` fields were null because Avalonia's x:Name source generator did not emit backing fields for the newly added elements.
- **Fix:** Replaced direct field access with `this.FindControl<TextBlock>("WindrosePlusLicenseText")` / `FindControl<Border>("WindrosePlusLicenseBox")`, matching the pre-existing pattern used for `VersionText` in the constructor of the same dialog.
- **Files modified:** src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml.cs
- **Verification:** User re-ran smoke test → license toggle works, GitHub button opens browser, DE/EN switch works — approved.
- **Committed in:** fb5ac48

**2. [Plan path drift] Strings files path corrected**
- **Found during:** Task 2
- **Issue:** Plan referenced `Resources/Strings.de.axaml`; project actually uses `Resources/Strings/Strings.de.axaml`.
- **Fix:** Edited the actual files at the correct path. No structural change.
- **Files modified:** Resources/Strings/Strings.{de,en}.axaml
- **Verification:** DE/EN switch in running app flips all new keys correctly (smoke step 7).
- **Committed in:** e1f1d39

---

**Total deviations:** 2 (1 auto-fixed bug, 1 path correction)
**Impact on plan:** NRE fix was essential (feature was broken until then). Path correction was trivial and transparent. No scope creep.

## Issues Encountered

- Avalonia x:Name field generation is inconsistent for newly added elements after an incremental build — FindControl is the reliable fallback and is now the preferred pattern for this dialog.

## User Setup Required

None — no external service configuration. The GitHub URL opens via the OS default browser; no token or credential is required.

## Next Phase Readiness

- WindrosePlusService is DI-resolvable everywhere — Phase 9 (Wizard + Retrofit) can inject it directly into the wizard ViewModel and retrofit dialog without further plumbing
- Launcher-switch is driven entirely by AppSettings per-server maps — Phase 9 only needs to flip the flag after install/opt-in; no ServerProcessService changes required
- Phase 10 (Health) can reuse the Third-Party Licenses pattern for any additional bundled dependencies
- Localization key namespace (`About.*`, `Warning.WindrosePlus*`, `Error.WindrosePlus*`) is established — later phases extend it

**Blockers:** None.

## Self-Check: PASSED

- Task commits exist: 4d5eddd, e1f1d39, fb5ac48 (verified via `git log`)
- App.axaml.cs line 75 contains the AddSingleton registration (grep confirmed)
- WindrosePlus-LICENSE.txt embedded as AvaloniaResource (stat + csproj confirmed in Task 2 commit)
- ServerProcessServiceLauncherTests.cs created with 3 [Fact]s (Task 1 commit adds 122 lines)
- Human checkpoint approved 2026-04-19

---
*Phase: 08-windroseplus-bootstrap*
*Completed: 2026-04-19*
