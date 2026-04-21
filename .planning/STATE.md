---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: milestone
status: completed
last_updated: "2026-04-21T00:00:00Z"
last_activity: "2026-04-21 — Phase 12 executed: opt-out Empty States für Players/Events/SeaChart/Editor — IsWindrosePlusActive guard + InstallWindrosePlusCommand + i18n (DE+EN) + Panel-Overlay in allen 4 Views"
progress:
  total_phases: 5
  completed_phases: 5
  total_plans: 13
  completed_plans: 13
---

# State

## Current Position

Phase: 11 — Feature Views
Plan: 05 — COMPLETE
Status: Plan 11-05 complete — Config Editor shipped: ConfigEntryViewModel (inline validation via WindrosePlusConfigSchema.Validate, ToTypedValue, JsonElement handling), EditorViewModel (LoadAsync grouped by Category, SaveCommand gated on CanSave, WriteConfigAsync atomic, RestartRequired toast when Running), EditorView.axaml (grouped ItemsControl, BrandErrorBrush inline errors, Save button), i18n 7 keys DE+EN. EDITOR-01, EDITOR-02, EDITOR-03 satisfied. 155/155 tests pass, 0 build errors.
Last activity: 2026-04-20 — Plan 11-05 executed (Config Editor view: grouped inline-validated editor for windrose_plus.json)

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
| 9. Opt-in UX (Wizard + Retrofit) | ✅ Complete — All 3 plans done. WIZARD-01..04 + RETRO-01..03 satisfied. Retrofit banner + dialog + Settings WP card shipped. |
| 10. Health & Support | ✅ Complete — All 2 plans done. HEALTH-01 + HEALTH-02 satisfied. Health banner + report URL + rate-limit + grace period shipped. |
| 11. Feature Views | ✅ Complete — All 5 plans done. PLAYER-01..04 + EVENT-01..03 + SEACHART-01..02 + EDITOR-01..03 satisfied. |
| 12. Empty States (Opt-out UX) | ✅ Complete — EMPTY-01 + EMPTY-02 satisfied. IsWindrosePlusActive guard + InstallWindrosePlusCommand + opt-out Panel overlay in all 4 views + i18n DE+EN. |

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

## Decisions (Plan 09-03)

- `RetrofitBannerViewModel` is NOT registered in DI — takes `string serverInstallDir` at construction time; DashboardViewModel creates it via `new` using its own injected services (IWindrosePlusService, IAppSettingsService, IToastService)
- `StateChanged` C# event on RetrofitBannerViewModel triggers `DashboardViewModel.RefreshAsync` on dismiss/install-complete — avoids timer-lag between user action and banner disappearance; no MediatR needed (local scope, single subscriber)
- Dialog cancel leaves `OptInState=NeverAsked` (banner reappears next refresh); only `ShowDialog<bool>(true)` result proceeds to InstallAsync — no silent persist on cancel (RETRO-03)
- No close-X on banner (RETRO-03 compliance): only explicit "Jetzt installieren" or "Nicht jetzt" exits are provided
- WindrosePlus Settings card added (extra, Rule 2 deviation): users who dismissed "Nicht jetzt" need a stable re-entry point to install/manage WP without JSON editing

## Decisions (Plan 10-01)

- Both helpers are `public static` classes — no `InternalsVisibleTo` needed; test project reaches them directly via ProjectReference
- `port <= 0` guard (not `== 0`) in HealthCheckHelper to cover negative port edge cases
- Linked CTS with `CancelAfter(3s)` as inner safety-net independent of caller token
- ReportUrlBuilder uses C# 8+ range slice `l[..MaxLogLineChars]` for truncation
- No InternalsVisibleTo added to Core.csproj — all types are public

## Decisions (Plan 10-02)

- HealthBannerViewModel receives windroseBuildId as `string?` ctor param instead of IServerConfigService — `ServerDescription.DeploymentId` is on the outer envelope class (`ServerDescriptionFile`), not on the inner class returned by `LoadServerDescriptionAsync`; `InstallInfo.BuildId` is the correct Windrose version proxy
- IHttpClientFactory injected into DashboardViewModel (already registered via `s.AddHttpClient()` in App.axaml.cs — no new DI registration needed)
- Health check block placed inside the `!string.IsNullOrWhiteSpace(serverDir)` branch after the retrofit banner block — reuses same `serverDir` variable
- `danger` button class used for "Report Issue" — matches existing usage in ConfigurationView/BackupsView/ModsView

## Decisions (Plan 11-01)

- `WindrosePlusApiService` uses `ILogger<WindrosePlusApiService>` from `Microsoft.Extensions.Logging` (not Serilog static `Log`) — Core.csproj has no Serilog package; consistent with WindrosePlusService precedent
- Port guard uses `port <= 0` (not `== 0`) to defensively cover negative port edge cases (matches Phase 10 HealthCheckHelper precedent)
- `EventsLogParser.TryParseLine` only accepts `type = "join" | "leave"` (case-insensitive); unknown types return null — safe extensibility for future event types
- `WriteConfigAsync` uses atomic `.tmp` then `File.Move(overwrite:true)` pattern — matches established `EnsureArchiveCachedAsync` pattern in `WindrosePlusService`
- `WindrosePlusApiService` takes `ILogger<T>` as third constructor param — DI injects it automatically; tests use `NullLogger<WindrosePlusApiService>.Instance`

## Decisions (Plan 11-04)

- `PointerPressed` on Ellipse instead of `TapGestureRecognizer`: `<TapGestureRecognizer>` caused Avalonia AVLN2000 compile error (type not resolvable); PointerPressed directly on element achieves the same result
- `x:DataType="vm:PlayerMarkerViewModel"` on `ControlTheme` for Canvas.Left/Top setters: required for Avalonia compiled binding engine to resolve CanvasX/CanvasY in ItemContainerTheme scope
- Toggle deselect on repeat click: `ReferenceEquals(SelectedMarker, marker) ? null : marker` — tapping same marker closes detail panel
- Auto-expanding world bounds start at ±30000: low confidence on actual Windrose world extent; bounds grow as player position data arrives each poll

## Decisions (Plan 11-03)

- `EnableRowVirtualization` removed from EventsView.axaml — this is a WPF-only property; Avalonia DataGrid enables row virtualization by default with no attribute required
- `EventsViewModel` ctor takes `IWindrosePlusApiService` as future extension point (not used in Plan 03); Plan 02 may use it for kick/ban integrations
- Skeleton ViewModels/Views for Plans 11-02/11-04/11-05 (PlayersViewModel, SeaChartViewModel, EditorViewModel + Views) committed in this plan because linter auto-updated MainWindowViewModel to reference all 4 Phase-11 nav types simultaneously — staged to keep build green

## Decisions (Plan 11-02)

- DiffUpdate avoids ObservableCollection.Clear() on each poll tick — prevents selection loss and reduces UI jank during refresh cycles
- BanDialogResult(int? Minutes) record: null=permanent, int=timed duration — clean discriminated-union pattern without enum, pattern-matched in BanAsync
- Watermark attribute kept on TextBox (matching project-wide convention) instead of PlaceholderText

## Decisions (Plan 11-05)

- `CategoryGroup` helper class defined inline in `EditorViewModel.cs` — simple data holder, no separate file needed
- `FormatValue` special-cases `System.Text.Json.JsonElement` — STJ deserializes `Dictionary<string, object?>` values as `JsonElement`; must unwrap per ValueKind for correct string display
- `SaveCommand.NotifyCanExecuteChanged` called from each entry's `PropertyChanged` handler for `HasError` — keeps button disabled state in sync with per-entry validation
- `Editor.Subtitle` skeleton key from Plan 11-01 removed from both DE+EN string files — replaced by 7 functional keys
- `BrandErrorBrush` confirmed from `WindrosePlusOptInControl.axaml` usage — project convention for error foreground

## Blockers

None.

## Next Step

Milestone v1.2 complete — all 5 phases done, all 27 requirements satisfied. Ready for release: `/gsd:complete-milestone`.
