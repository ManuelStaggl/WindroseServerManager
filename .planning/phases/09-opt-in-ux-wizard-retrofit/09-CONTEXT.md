# Phase 9: Opt-in UX (Wizard + Retrofit) - Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

User-facing opt-in surface for WindrosePlus. Delivers (a) a new-server install wizard whose WindrosePlus step is default-on with one-click opt-out, generates a secure RCON password, captures the admin Steam-ID, and picks a free local dashboard port; and (b) a retrofit flow for servers carried over from v1.0/v1.1 that offers WindrosePlus installation per server via a non-modal Dashboard banner with persisted three-state decision (never-asked / opted-in / opted-out). Consumes `IWindrosePlusService` from Phase 8. Health banner (Phase 10), feature views (Phase 11), and empty-state CTAs (Phase 12) are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Wizard Shape & Placement
- **Form:** New modal stepper dialog (FluentWindow) — **not** a redesign of `InstallationView`. Suggested flow: Install path → WindrosePlus (default-on, feature list, secrets) → Confirm.
- **Dedicated ViewModel:** New `InstallWizardViewModel` owns the flow. On confirm it calls `IServerInstallService` (SteamCMD) then `IWindrosePlusService.InstallAsync` in sequence. Existing `InstallationViewModel` / `InstallationView` stays for re-install / status / updates.
- **Timing:** WindrosePlus decision + secrets are collected **before** SteamCMD starts. SteamCMD and WindrosePlus install then run unattended to completion.
- **Re-install scope:** Wizard runs only when `ServerInstallInfo.IsInstalled == false` (fresh install). Re-installs keep the existing single-page `InstallationView` and preserve the prior opt-in decision. Consistent with RETRO-03 "never silently."
- **Trigger:** `InstallationView` gets a "New server" entry point that opens the wizard; existing "Install/Update" button stays for re-install semantics.

### RCON Password UX
- Auto-generated on wizard entry (cryptographically random, URL-safe, ≥24 chars).
- Displayed **masked** by default with eye-toggle reveal, **copy-to-clipboard** button, and **regenerate** button.
- Stored in AppSettings per-server map (see Storage below) and written into `windrose_plus.json[RCON]` at install time (WindrosePlus reads its own config from that file — per `project_windroseplus_maintainer_agreement` memory).
- Tooltip explains the password is used for WindrosePlus admin HTTP commands.

### Dashboard Port UX
- Auto-picked from a free port in range **18080–18099** on wizard entry. First free port wins.
- Shown read-only in the wizard with a "change in Settings" caption.
- Re-picked automatically on server launch if a conflict is detected at runtime (future safeguard, not a wizard decision).
- Never hardcoded — satisfies WIZARD-04.

### Admin Steam-ID UX
- Single text input accepts **either** a raw SteamID64 (17 digits) **or** a full Steam profile URL (`steamcommunity.com/id/...` or `steamcommunity.com/profiles/7656119...`). App extracts the SteamID64 via regex — no Steam Web API call.
- Inline validation on blur: format-valid vs invalid. No network lookup — vanity URLs without a profile number are rejected with a message pointing the user to the numeric URL.
- Required field in the wizard (WIZARD-03).

### Storage (new per-server fields)
- **Pattern:** Mirror Phase 8's AppSettings per-server dictionaries (`WindrosePlusActiveByServer`, `WindrosePlusVersionByServer`). Add:
  - `WindrosePlusRconPasswordByServer : Dictionary<string,string>`
  - `WindrosePlusDashboardPortByServer : Dictionary<string,int>`
  - `WindrosePlusAdminSteamIdByServer : Dictionary<string,string>`
  - `WindrosePlusOptInStateByServer : Dictionary<string, OptInState>` where `OptInState ∈ {NeverAsked, OptedIn, OptedOut}` (for retrofit tri-state — see below).
- `ServerInstallInfo` record extended with read-through properties for these (consistent with Phase 8's WindrosePlusActive/VersionTag treatment).
- Install writes the password + port + admin SteamID into `windrose_plus.json` inside the server directory (single source for the running server); AppSettings is the app-side mirror so the wizard/retrofit can display values without re-parsing the JSON.

### Retrofit Flow
- **Surface:** Non-modal `InfoBar`-style banner at top of `DashboardView`, **per server** (appears when the currently selected server has `WindrosePlusOptInState == NeverAsked` and `WindrosePlusActive == false`). No app-launch queue dialog, no on-navigation prompt.
- **Actions:** "Install WindrosePlus" (primary) opens an inline retrofit dialog that reuses the wizard's WindrosePlus step (feature grid + Steam-ID + auto-generated password/port). "Not now" (subtle) sets state to `OptedOut`.
- **No close-X:** Banner has no dismiss button — every interaction is an explicit decision. Satisfies RETRO-03.
- **Post-decision:**
  - `OptedIn` → install runs, banner disappears.
  - `OptedOut` → banner hidden permanently for that server. User can revisit via Settings toggle OR via Phase 12 empty-state CTAs in WindrosePlus-dependent views.
- **Migration rule (first v1.2 launch):** Every existing server (from v1.0/v1.1) is initialized to `OptInState.NeverAsked`. No silent upgrade. Matches RETRO-01.

### Feature List & Copy
- **Layout:** 6-tile icon grid — 👤 Players, 🚫 Kick & Ban, 📢 Broadcast, 📜 Events, 🗺️ Sea-Chart, ⚙️ INI-Editor. Used identically in wizard step and retrofit dialog (shared UserControl).
- **Link:** "WindrosePlus by HumanGenome · MIT · Learn more ↗" caption underneath the grid; opens `https://github.com/HumanGenome/WindrosePlus` in browser via `OpenUrl` helper.
- **Tone:** Plain, neutral — matches rest of the app. No pirate/nautical theming. DE/EN strings parallel.
- **Disclaimer:** One-liner near the opt-in toggle: "WindrosePlus is a separate MIT-licensed mod. It installs into your server's game files and can be disabled at any time from Settings."
- **Bilingual from day one:** Extend the Phase 8 `About.WindrosePlus.*` namespace with `Wizard.WindrosePlus.*`, `Retrofit.WindrosePlus.*`, `Feature.*` keys in `Resources/Strings/Strings.de.axaml` + `Strings.en.axaml`.

### Claude's Discretion
- Exact wizard step count and titles (2 vs 3 steps acceptable as long as the flow above holds).
- Concrete password character set (must be URL-safe, ≥24 chars).
- Regex(es) for Steam-ID parsing — include a 5s timeout per project coding standards.
- InfoBar styling / icon selection for the retrofit banner (follow existing Toast/empty-state visual language).
- Whether the wizard-step UserControl is reused literally in the retrofit dialog or duplicated with minor layout differences.
- Error-state copy for install failures during the wizard (follow ErrorMessageHelper patterns).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project planning
- `.planning/PROJECT.md` — Stack, constraints, v1.2 scope
- `.planning/REQUIREMENTS.md` — WIZARD-01..04 and RETRO-01..03 requirement text
- `.planning/ROADMAP.md` §"Phase 9: Opt-in UX (Wizard + Retrofit)" — Goal + 4 success criteria
- `.planning/STATE.md` — Accumulated v1.2 context

### Phase 8 handoff (consumed, not re-decided)
- `.planning/phases/08-windroseplus-bootstrap/08-CONTEXT.md` — Decisions on cache/version strategy, launcher switch, MIT bundling
- `src/WindroseServerManager.Core/Services/IWindrosePlusService.cs` — `FetchLatestAsync`, `InstallAsync`, `ResolveLauncher`, `ReadVersionMarker`
- `src/WindroseServerManager.Core/Services/WindrosePlusService.cs` — Concrete impl (reference for `InstallProgress` stages consumed by wizard progress UI)
- `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` — Extend with new fields / read-through props

### Existing code to integrate with
- `src/WindroseServerManager.App/Views/Pages/InstallationView.axaml(.cs)` + `ViewModels/InstallationViewModel.cs` — Gets a "New server" entry point; existing single-page flow stays
- `src/WindroseServerManager.App/Views/Dialogs/ConfirmDialog.axaml` — Reference pattern for FluentWindow modal dialogs (styling, BrandNavySurface brushes)
- `src/WindroseServerManager.App/Views/Dialogs/AboutDialog.axaml(.cs)` — Reference pattern for multi-section dialog + `FindControl<T>` usage (see Plan 08-03 decision)
- `src/WindroseServerManager.App/Services/ToastService.cs` + `ErrorMessageHelper.cs` — Error/success feedback during install
- `src/WindroseServerManager.App/Services/LocalizationService.cs` + `Resources/Strings/Strings.{de,en}.axaml` — Bilingual strings
- `src/WindroseServerManager.App/Views/Pages/DashboardView.axaml` + `ViewModels/DashboardViewModel.cs` — Host of the retrofit banner
- `src/WindroseServerManager.App/App.axaml.cs` — DI registration for `InstallWizardViewModel` + retrofit services

### External
- `https://github.com/HumanGenome/WindrosePlus` — Feature list source of truth; `windrose_plus.json[RCON]` schema (see `project_windroseplus_maintainer_agreement` memory)
- Steam Community URL format for SteamID64 extraction (numeric `/profiles/<id>` path)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **FluentWindow modal dialog pattern** (`ConfirmDialog`, `AboutDialog`): `Background="{DynamicResource BrandNavySurfaceBrush}"`, rounded Border, CenterOwner, `x:Name` elements accessed via `FindControl<T>` (Plan 08-03 decision).
- **IWindrosePlusService.InstallAsync**: Already supports `IProgress<InstallProgress>` + `CancellationToken` — wizard progress stage reuses this.
- **AppSettings per-server dictionaries**: Phase 8 added `WindrosePlusActiveByServer` / `WindrosePlusVersionByServer`. Extend identically.
- **ErrorMessageHelper + ToastService**: Consistent error surface for install failures and validation messages.
- **LocalizationService + Strings.de/en.axaml**: Bilingual plumbing is in place; just add new keys.

### Established Patterns
- **ServerInstallInfo as an immutable record with read-through props** — extend for RconPassword / DashboardPort / AdminSteamId / OptInState.
- **Phase 8 namespace convention** — `About.WindrosePlus.*`, `Warning.WindrosePlus*`, `Error.WindrosePlus*`. Phase 9 adds `Wizard.WindrosePlus.*`, `Retrofit.WindrosePlus.*`, `Feature.*`.
- **Chromeless FluentWindow dialogs** sized via `SizeToContent="Height"` + fixed Width — suitable baseline for the wizard window (expect ~560–640 wide for the stepper).
- **No existing wizard infrastructure**: Phase 9 introduces the first multi-step dialog in the app — treat as new primitive, not a retrofit of existing dialogs.

### Integration Points
- `InstallationView` gains a "New server" button that opens the wizard (keeps re-install flow untouched).
- `DashboardView` gains a top-of-page banner zone bound to a new `DashboardViewModel.RetrofitBannerVisible` + `RetrofitBannerVm` slot.
- DI: register `InstallWizardViewModel` (Transient — per install), `RetrofitBannerViewModel` (Transient per server) alongside the Phase 8 registrations in `App.axaml.cs`.
- AppSettings JSON schema grows four dictionaries — migration on first v1.2 launch seeds `OptInState = NeverAsked` for existing servers.

</code_context>

<specifics>
## Specific Ideas

- **Shared WindrosePlus step UserControl**: Single `WindrosePlusOptInControl` (feature grid + disclaimer + secrets form) used inside both the wizard step and the retrofit dialog. Keeps copy + visuals in sync and is cheaper than two near-duplicate views.
- **"Never silent" is a hard rule**: Wizard default-on is fine (WIZARD-02), but the user still sees and confirms the WindrosePlus step before install starts. Retrofit banner has no close-X. Both rules trace to RETRO-03.
- **Port range 18080–18099**: Small window avoids clashes with WindrosePlus defaults while leaving headroom for future multi-instance users. If exhausted, fall back to OS-assigned ephemeral port.
- **Steam profile URL parsing**: Keep to regex. No Steam Web API — avoids another third-party dependency and respects the Nexus-API-removed principle from v1.1.

</specifics>

<deferred>
## Deferred Ideas

- **Health-check banner** (HEALTH-01/02) — Phase 10; distinct from retrofit banner.
- **Empty-state CTAs in feature views** — Phase 12 (EMPTY-01/02); re-uses the retrofit dialog from this phase.
- **Settings-page per-server WindrosePlus toggle** — Mentioned as the "revisit" path, but actual Settings UI lives with Phase 12 empty states / a dedicated settings polish pass. Phase 9 only needs AppSettings persistence + the banner/dialog entry points.
- **Background upstream version check / auto-upgrade** — UPGRADE-01, v1.3.
- **Change WindrosePlus state on existing running server** — Flag flip + restart prompt is a Phase 11 editor concern.
- **Steam Web API vanity-URL resolution** — Out of scope; user supplies numeric URL or raw SteamID64.

</deferred>

---

*Phase: 09-opt-in-ux-wizard-retrofit*
*Context gathered: 2026-04-19*
