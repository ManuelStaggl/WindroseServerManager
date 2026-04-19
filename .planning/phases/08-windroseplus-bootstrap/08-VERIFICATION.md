---
phase: 08-windroseplus-bootstrap
verified: 2026-04-19T00:00:00Z
status: human_needed
score: 4/4 success criteria verified (automated)
re_verification:
  previous_status: none
  previous_score: n/a
human_verification:
  - test: "About dialog visual smoke test"
    expected: "Open Settings → About. The 'Lizenzen von Drittanbietern' / 'Third-Party Licenses' section is visible; WindrosePlus name appears verbatim; clicking 'WindrosePlus auf GitHub' opens browser to https://github.com/HumanGenome/WindrosePlus; clicking 'Lizenztext anzeigen' reveals license box starting with 'MIT License'; language toggle updates labels."
    why_human: "Visual layout, browser redirect behavior, and DynamicResource language switching cannot be grep-verified. Phase 3's checkpoint task (08-03 Task 3) is flagged [APPROVED 2026-04-19] in the plan, but that approval predates final build verification — a fresh smoke run is recommended before shipping."
  - test: "Live launcher switch against a real Windrose server install"
    expected: "With a real Windrose server installed: toggle AppSettings.WindrosePlusActiveByServer[<path>]=true, drop a dummy StartWindrosePlusServer.bat, restart app, click Start → log line shows the .bat path (not the .exe). Toggle false → starts WindroseServer.exe."
    why_human: "Requires a real server install on disk and observable process start behavior; unit tests cover ResolveLauncher pure-function logic but not the full Start pipeline."
---

# Phase 8: WindrosePlus Bootstrap — Verification Report

**Phase Goal:** "The app can download, cache, install, and launch WindrosePlus on any server the user owns — establishing the `WindrosePlusService` that every later phase consumes."
**Verified:** 2026-04-19
**Status:** human_needed (all automated checks PASS; visual/live smoke flagged)
**Re-verification:** No — initial verification.

## Goal Achievement

Verified against the four ROADMAP Success Criteria (treated as the authoritative truths, since they were explicitly defined in get-phase).

### Observable Truths (from ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | App fetches latest WindrosePlus release from GitHub; cache used as offline fallback | VERIFIED | `WindrosePlusService.FetchLatestAsync` calls `https://api.github.com/repos/HumanGenome/WindrosePlus/releases/latest` via `IHttpClientFactory`; writes JSON to `_metadataCachePath`; on failure parses cache. Tests `FetchLatest_ParsesTagAndDigest`, `FetchLatest_AcceptsMissingDigest_WithWarning`, `Install_UsesCache_WhenApiUnreachable_AndCacheExists`, `Install_ThrowsOfflineInstallException_WhenNoCache` — ALL GREEN. |
| 2 | Install produces working UE4SS + WindrosePlus payload in server game-binaries dir; does not break WindroseServer.exe path | VERIFIED | `InstallAsync` extracts WindrosePlus to tempRoot, extracts UE4SS into `R5/Binaries/Win64`, atomically merges via `File.Move` (overwrite), preserves user config (`windrose_plus.json`, etc.), writes `.wplus-version` marker. Tests `Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir`, `Install_PreservesExistingUserConfig`, `Install_OverwritesVendorBinaries`, `Install_WritesVersionMarker`, `Install_ThrowsShaMismatch_WhenArchiveModified` — ALL GREEN. |
| 3 | WindrosePlus MIT LICENSE (HumanGenome) present in install output AND visible in About dialog; name "WindrosePlus" appears verbatim | VERIFIED (automated) / human-needed (visual) | Install: `Install_CopiesLicenseToServerDir` test green — LICENSE copied as `WindrosePlus-LICENSE.txt`. About dialog: `src/WindroseServerManager.App/Resources/Licenses/WindrosePlus-LICENSE.txt` starts with "MIT License"; registered as `<AvaloniaResource>` in `.csproj`; `AboutDialog.axaml` binds `About.ThirdPartyLicenses.Heading`, shows repo link `https://github.com/HumanGenome/WindrosePlus`, and has ShowLicense button wired to `OnShowWindrosePlusLicenseClick` in code-behind. Name appears verbatim (not rebranded) in all DE+EN strings. **Visual rendering needs human verification.** |
| 4 | Launcher automatically switches: `.bat` when WindrosePlusActive, `.exe` when opted out | VERIFIED | `WindrosePlusService.ResolveLauncher` is pure-function decision; `ServerProcessService` constructor injects `IWindrosePlusService`, `StartAsync` and `ValidateCanStart` both call `_windrosePlus.ResolveLauncher(dir, info)`. Tests `ResolveLauncher_OptedOut_ReturnsExe`, `ResolveLauncher_Active_ReturnsBat`, `ResolveLauncher_Active_BatMissing_FallsBackWithWarning` plus the 3 `ServerProcessServiceLauncherTests` — ALL GREEN. |

**Score:** 4/4 truths verified by automated tests. Truths 3 also flagged for human visual verification.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/WindroseServerManager.Core/Services/IWindrosePlusService.cs` | Interface contract | VERIFIED | 31 lines, declares `FetchLatestAsync`, `InstallAsync`, `ResolveLauncher`, `ReadVersionMarker`, plus `WindrosePlusOfflineException` + `WindrosePlusDigestMismatchException`. Imported by `WindrosePlusService` and `ServerProcessService`. |
| `src/WindroseServerManager.Core/Services/WindrosePlusService.cs` | Full implementation | VERIFIED | 515 lines (> 250 min). `public sealed class WindrosePlusService : IWindrosePlusService`. Contains `SemaphoreSlim`, `SHA256.HashDataAsync`, multiple `File.Move` atomic renames, structured Serilog templates (no interpolation in log calls). |
| `src/WindroseServerManager.Core/Models/WindrosePlusRelease.cs` | Release DTO | VERIFIED | `sealed record` with Tag, AssetName, DownloadUrl, SizeBytes, DigestSha256. |
| `src/WindroseServerManager.Core/Models/WindrosePlusInstallResult.cs` | Install result DTO | VERIFIED | Present. |
| `src/WindroseServerManager.Core/Models/WindrosePlusVersionMarker.cs` | Version marker | VERIFIED | Present, JSON-serializable. |
| `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` | Extended with WindrosePlusActive | VERIFIED | Contains `WindrosePlusActive` + `WindrosePlusVersionTag` properties. |
| `src/WindroseServerManager.Core/Models/AppSettings.cs` | Extended with per-server dictionaries | VERIFIED | Contains `WindrosePlusActiveByServer` + `WindrosePlusVersionByServer`. |
| `src/WindroseServerManager.App/Resources/Licenses/WindrosePlus-LICENSE.txt` | Embedded MIT LICENSE | VERIFIED | Starts with "MIT License". Registered in `.csproj` at line 25 as `<AvaloniaResource>`. |
| `src/WindroseServerManager.App/App.axaml.cs` | DI registration | VERIFIED | Line 75: `s.AddSingleton<IWindrosePlusService, WindrosePlusService>();` |
| `src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml` | Third-Party Licenses section | VERIFIED | 8 WindrosePlus-related references (heading, intro, repo link, license button, license box, etc.). |
| `src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml.cs` | ShowLicense handler | VERIFIED | `OnShowWindrosePlusLicenseClick` method present. |
| `tests/.../WindrosePlusServiceTests.cs` | 13 behavior tests | VERIFIED | All green; 0 `Skip =` remaining. |
| `tests/.../ServerProcessServiceLauncherTests.cs` | 3 integration tests | VERIFIED | 3 `[Fact]` methods; all green. |
| `tests/.../Fixtures/FakeGithubReleaseServer.cs` + `TempServerFixture.cs` + `SampleArchiveBuilder.cs` | Test doubles | VERIFIED | Present and consumed by tests. |
| `src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml` + `Strings.en.axaml` | 6 new bilingual keys | VERIFIED | `About.ThirdPartyLicenses.Heading` and `Warning.WindrosePlusBatMissing` present in both files. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `ServerInstallInfo` | `AppSettings` | `WindrosePlusActive` flag persisted via `WindrosePlusActiveByServer` dict | WIRED | Both models contain the properties; `ServerProcessService.BuildInstallInfo` reads the dict keyed by full path. |
| `WindrosePlusService.InstallAsync` | GitHub Releases API | `IHttpClientFactory` | WIRED | Constants `WindrosePlusApiUrl` and `Ue4ssApiUrl` target the exact api.github.com paths. |
| `WindrosePlusService.InstallAsync` | server install dir | Atomic `File.Move` from same-volume temp | WIRED | `AtomicMergeIntoServer` uses `File.Move(..., overwrite: true)` after `ZipFile.ExtractToDirectory` into a `Path.GetDirectoryName(...)`-sibling temp root (same volume). |
| `ServerProcessService.StartAsync` | `IWindrosePlusService.ResolveLauncher` | Constructor-injected singleton | WIRED | Field `_windrosePlus`; StartAsync line 80 and ValidateCanStart line 52 both call `_windrosePlus.ResolveLauncher(dir, info)`. |
| `AboutDialog.axaml` | Embedded LICENSE | `avares://` URI loaded in code-behind | WIRED | `OnShowWindrosePlusLicenseClick` opens the avares URI and renders into `WindrosePlusLicenseText`. |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|----------------|-------------|--------|----------|
| WPLUS-01 | 08-01, 08-02 | GitHub fetch + local cache offline fallback | SATISFIED | `FetchLatestAsync` + cache-fallback tests green. |
| WPLUS-02 | 08-01, 08-02 | Install produces working UE4SS+WindrosePlus payload | SATISFIED | `InstallAsync` + atomic-install/preserve-config/marker tests green. |
| WPLUS-03 | 08-01, 08-02, 08-03 | MIT LICENSE bundled and shown in About; "WindrosePlus" verbatim | SATISFIED (automated) / visual-human | LICENSE embedded; About dialog XAML+code-behind wire the reveal; strings present in DE+EN; install copies `WindrosePlus-LICENSE.txt`. Visual rendering flagged for human. |
| WPLUS-04 | 08-01, 08-02, 08-03 | Auto launcher switch (.bat vs .exe) per-server | SATISFIED | `ResolveLauncher` + `ServerProcessService` integration tests green (3/3 + 3/3 sub-cases in service tests). |

No orphaned requirements — every WPLUS-0x ID declared in ROADMAP is claimed by at least one plan's `requirements` frontmatter and has matching implementation evidence.

### Anti-Patterns Found

None. Scan of `WindrosePlusService.cs` returned no `TODO`, `FIXME`, `PLACEHOLDER`, or `NotImplementedException`. `Skip =` markers fully removed from `WindrosePlusServiceTests.cs` (all 13 tests active and green). Pre-existing warnings in `ModServiceTests.cs` (CS0067, xUnit1031) are unrelated to Phase 8.

### Test Run Summary

```
dotnet test --filter "FullyQualifiedName~WindrosePlus|FullyQualifiedName~ServerProcessServiceLauncher|FullyQualifiedName~AppSettingsTests"
Bestanden! : Fehler: 0, erfolgreich: 18, übersprungen: 0, gesamt: 18
```

### Human Verification Required

See frontmatter `human_verification` block. Two items:
1. Visual smoke of About dialog (layout, language switch, link click, license reveal).
2. Live launcher switch on a real server install (optional but recommended before shipping Phase 8).

Plan 08-03 Task 3 is marked `status="approved"` with timestamp `[APPROVED 2026-04-19]` in the plan, so the human checkpoint was already gated. A re-run against the current HEAD build is nevertheless recommended because the codebase has been modified since (wc -l shows 515 lines in service — final shape).

### Gaps Summary

No blocking gaps. Goal "The app can download, cache, install, and launch WindrosePlus" is achieved at the code level across all four Success Criteria. Status is `human_needed` purely because:
- About dialog visual rendering and language-toggle behavior cannot be grep-verified.
- End-to-end launcher switch against a real server install is not exercised by unit tests (pure-function `ResolveLauncher` is covered, but the spawned-process path is not).

Both are standard human-smoke items, not defects.

---

_Verified: 2026-04-19_
_Verifier: Claude (gsd-verifier)_
