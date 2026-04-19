---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: milestone
status: in-progress
last_updated: "2026-04-19T19:00:00Z"
last_activity: 2026-04-19 — Plan 09-02 executed (2 feat + 5 fix commits, wizard smoke-tested, WIZARD-01/02 complete)
progress:
  total_phases: 5
  completed_phases: 1
  total_plans: 6
  completed_plans: 5
---

# State

## Current Position

Phase: 9 — Opt-in UX (Wizard + Retrofit)
Plan: 03 — next (retrofit dialog); Plans 01 + 02 complete
Status: Phase 9 wizard complete — InstallWizardWindow + WindrosePlusOptInControl + IWindrosePlusOptInContext shipped
Last activity: 2026-04-19 — Plan 09-02 executed (2 feat + 5 fix commits, wizard smoke-tested, WIZARD-01/02 complete)

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)
See: .planning/ROADMAP.md (v1.2 — phases 8–12)
See: .planning/REQUIREMENTS.md (27 v1.2 requirements, fully mapped)

**Core value:** One-click, end-to-end management of a Windrose dedicated server on Windows.
**Current focus:** v1.2 — WindrosePlus Integration (player management, events, sea chart, config editor)

## Progress

| Phase | Status |
|-------|--------|
| 8. WindrosePlus Bootstrap | ✅ Complete — All 3 plans done (contract + service impl + DI wiring/About-dialog). WPLUS-01…04 satisfied. |
| 9. Opt-in UX (Wizard + Retrofit) | 🟦 In progress — Plan 01 complete (foundation: data model + helpers + migration). Plans 02 (wizard) + 03 (retrofit) pending. WIZARD-03/04 + RETRO-01 satisfied. |
| 10. Health & Support | Not started |
| 11. Feature Views | Not started |
| 12. Empty States (Opt-out UX) | Not started |

## Accumulated Context

Carried over from v1.0 and v1.1:
- v1.0.0 shipped on Nexus/GitHub 2026-04-19 (phases 1–5)
- v1.1.0 shipped on GitHub 2026-04-19; Nexus mod #29 re-uploaded, awaiting moderator quarantine lift (phases 6–7)
- Nexus API removed entirely in v1.1 — "Open on Nexus" is URL-only; do NOT reintroduce API dependencies

v1.2 scope decisions:
- WindrosePlus is the core dependency — default-on with explicit opt-out, never silent
- Fetch-on-install via GitHub Releases API, cache locally for offline
- All WindrosePlus-dependent views (player list, events, sea chart, INI editor) must render clean empty states when opt-out is active, with CTA to install
- Phase 8 establishes a shared `WindrosePlusService` consumed by all later phases
- WindrosePlus collaboration proposal posted as GitHub issue at HumanGenome/WindrosePlus 2026-04-19 — user chose to proceed independently regardless of upstream response (MIT license covers bundling)

## Decisions (Plan 08-01)

- License filename bundled into server dir: `WindrosePlus-LICENSE.txt` (groups alphabetically with `.wplus-version` marker)
- Expected `WindrosePlusService` constructor signature for Plan 02: `(ILogger<WindrosePlusService> logger, IHttpClientFactory httpClientFactory, string cacheDir)`
- Per-server WP state keyed by full `InstallDir` path in `AppSettings.WindrosePlusActiveByServer` / `WindrosePlusVersionByServer`; `ServerInstallInfo` fields are read-through (no double-persistence)
- Wave-0 tests ship as `[Fact(Skip=...)]` against `IWindrosePlusService`; Plan 02 removes `Skip` args and instantiates the concrete class — minimal diff
- `FakeGithubReleaseServer.FailWindrosePlusAsset` toggle added to support the atomic-install-failure test (mid-download 500 simulation)

## Decisions (Plan 08-02)

- `WindrosePlusService` constructor widened to `(ILogger<WindrosePlusService>, IHttpClientFactory, string? cacheDir = null)` — nullable default resolves to `%LocalAppData%\WindroseServerManager\cache\windroseplus\`. Plan 01's non-nullable spec is a subset; production DI gains a trivial one-line registration.
- LICENSE copy runs BEFORE the atomic merge: the merge uses `File.Move` and empties `tempRoot`, so the LICENSE source file must be copied out first.
- UE4SS fetch is tolerant: if UE4SS API+cache both fail, install proceeds without UE4SS payload (warning logged). Phase 10 health banner surfaces missing UE4SS, not this service.
- Synthetic "cached" `WindrosePlusRelease` (Tag="cached", DownloadUrl="") activates only when API offline AND archive cache exists AND metadata cache absent — safety net for partial-cache seed scenarios.
- `LoggerAdapter<T> : ILogger<T>` bridges Plan 01's non-generic `TestLogger` into the concrete service's `ILogger<WindrosePlusService>` dependency.

## Decisions (Plan 09-01)

- `OptInState` enum serialized as string via `[JsonConverter(typeof(JsonStringEnumConverter))]` at the enum declaration (not per-property) — survives round-trip without repeating attributes on every `Dictionary<,>` using it
- Added second public `AppSettingsService(ILogger, string settingsPath)` ctor — `IAppSettingsService` interface unchanged; production DI still uses single-arg ctor; the new one is for tests and future portable-mode callers
- `MigrateToV12` retains orphan `OptInState` keys (server removed from `WindrosePlusActiveByServer` but decision persists) — strictly non-destructive, guarantees restored InstallDirs resurface the user's previous decision
- `ServerInstallInfo.NotInstalled` factory body untouched — all 4 new positional parameters have defaults so the 5-arg call still compiles
- Phase 9 test slice lives under `tests/WindroseServerManager.Core.Tests/Phase9/` and is selected via `--filter "FullyQualifiedName~Phase9"` — runs in ~100ms, 34 tests
- `FreePortProbe` fallback uses `TcpListener(IPAddress.Loopback, 0)` then `Stop()` before return — caller must bind immediately (documented in XML-doc)

## Decisions (Plan 08-03)

- DI registration lives at App.axaml.cs line 75, directly below `AddSingleton<ISteamCmdService, …>` — keeps Core-service block cohesive
- ServerProcessService reads AppSettings per-server maps via new `BuildInstallInfo(dir)` helper — single read-through point, no double-persistence
- About dialog: reused existing `BrandNavySurfaceAltBrush` / `BrandNavyBorderBrush` (no new brush introduced); license box is monospace 11px with MaxHeight 220 + ScrollViewer
- Strings live under `Resources/Strings/` (plan path `Resources/` was outdated — kept actual project convention)
- `OnShowWindrosePlusLicenseClick` uses `FindControl<T>("name")` instead of x:Name field access — Avalonia source generator did not emit fields for the new elements; FindControl matches the pre-existing `VersionText` pattern in the same dialog constructor
- Localization namespace established for WindrosePlus: `About.ThirdPartyLicenses.*`, `About.WindrosePlus.*`, `Warning.WindrosePlus*`, `Error.WindrosePlus*` — Phases 9-12 extend this

## Decisions (Plan 09-02)

- `IWindrosePlusOptInContext` defined as a separate interface file — UserControl code-behind casts DataContext to the interface only; both InstallWizardViewModel (Plan 02) and RetrofitDialogViewModel (Plan 03) implement it; zero VM-type coupling in the UserControl
- Feature grid uses compact 3×2 layout (3 columns, 2 rows) — fits inside 560 px wizard without vertical scroll at 100% DPI; original plan described 2×3
- Stepper rendered via three inline named Borders with Classes toggled from code-behind — simpler than an IntEqualsConverter + ItemsControl for a fixed 3-step flow
- SteamIdParser extended to explicitly recognize vanity `/id/` URLs and return null — needed for deterministic red-border UX; plan originally only mentioned generic invalid input handling
- Retrofit.* string keys (`Retrofit.Banner.Title`, `Retrofit.Banner.Body`, `Retrofit.Banner.Action.Install/Later`, `Retrofit.Dialog.Title`) ship in Plan 02 — Plan 03 reuses without adding new string keys
- `AddTransient<InstallWizardViewModel>` in DI — each wizard open resolves fresh instance with new RCON password + port probe; Singleton would share stale state across opens

## Blockers

None.

## Next Step

Phase 9 Plan 02 (wizard UI) complete. Next: Plan 09-03 — Retrofit dialog (RetrofitDialogViewModel implementing IWindrosePlusOptInContext, retrofit banner on DashboardView, reusing WindrosePlusOptInControl).
