# Phase 8: WindrosePlus Bootstrap - Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Infrastructure phase — establishes the shared `WindrosePlusService` that fetches WindrosePlus releases from GitHub, caches archives locally, installs UE4SS + WindrosePlus payload into a server's game-binaries directory, bundles the MIT LICENSE, and owns the launcher-switch between `StartWindrosePlusServer.bat` and `WindroseServer.exe`. Phase 8 delivers plumbing only — the wizard/retrofit UX (Phase 9), health check (Phase 10), and feature views (Phase 11) consume this service later.

</domain>

<decisions>
## Implementation Decisions

### Cache & Version Strategy
- **Cache location:** `%LocalAppData%\WindroseServerManager\cache\windroseplus\` — single shared cache across all servers
- **Version selection:** Always latest stable release from GitHub Releases API (no prereleases, no per-server pinning in v1.2 — pinning belongs to UPGRADE-01 in v1.3)
- **Cache semantics:** Every install attempts a live GitHub fetch; cache is only consulted as offline fallback (spec-aligned with WPLUS-01)
- **First install offline:** Hard-fail with a clear error message and a retry button — no silent degradation, no partial server creation

### UE4SS Handling
- **Source (REVISED 2026-04-19):** WindrosePlus v1.0.6 `WindrosePlus.zip` does NOT bundle UE4SS — its `install.ps1` fetches UE4SS from the `UE4SS-RE/RE-UE4SS` GitHub repo at install time. Decision: **replicate `install.ps1` fetch logic in C#** inside `WindrosePlusService`. Fetch WindrosePlus release + UE4SS release separately, merge both payloads during install. Do not shell out to PowerShell (breaks progress/cancel semantics). The UE4SS tag to fetch is the one `install.ps1` pins — read it from the script at build-time or pin it in a constants file; revisit if HumanGenome changes their bootstrap approach
- **Preexisting install:** Binaries are overwritten on re-install; user config files (WindrosePlus.json / .ini) are preserved
- **Extraction integrity:** Verify each downloaded archive against the `assets[].digest` field (SHA-256) from the GitHub Releases API. If digest is null (older tags), log a warning and continue. Extract into a temp directory on the SAME volume as the server install (cross-volume `File.Move` silently degrades to copy+delete — breaks atomicity), then atomic-move into the server directory

### Launcher Switch & Persistence
- **Flag storage:** The per-server "WindrosePlus active" flag lives in the existing per-server `ServerInstallInfo` / AppSettings structure. Migration rule: servers without the flag (carried over from v1.0/v1.1) are treated as opted-out until the Phase 9 retrofit dialog explicitly sets them
- **Missing .bat fallback:** If the flag says "active" but `StartWindrosePlusServer.bat` is missing, emit a Toast + log warning and launch `WindroseServer.exe`. No hard block — the Phase 10 health banner will surface the inconsistency
- **Integration with ServerProcessService:** `ServerProcessService` stays a dumb launcher and queries `WindrosePlusService` for the correct start-file path + args. All WindrosePlus-specific knowledge is encapsulated in the new service

### Install Contract & Version Tracking
- **Install API:** `async Task InstallAsync(server, IProgress<InstallProgress>, CancellationToken)` — mirrors the existing `SteamCmdService` pattern so the Phase 9 wizard and retrofit dialog share the progress surface
- **Version marker:** Write a `.wplus-version` file (or equivalent) into the install output containing the GitHub tag that was installed. Used by Phase 10's health report and as the foundation for v1.3's UPGRADE-01 — does not depend on WindrosePlus exposing its own `/version` endpoint

### About-Dialog License Exposure (WPLUS-03)
- Dedicated "Third-Party Licenses" section in the About dialog
- WindrosePlus MIT text rendered inline + link to `https://github.com/HumanGenome/WindrosePlus`
- The LICENSE file is also copied into the server install output (bundle requirement, independent of the About UI)
- Product name "WindrosePlus" appears verbatim everywhere — never rebranded

### Claude's Discretion
- Exact folder structure inside `%LocalAppData%\...\cache\windroseplus\` (flat vs per-version subdirs)
- Concrete `InstallProgress` stages / percentages
- GitHub API client: reuse existing HttpClient/UpdateCheckService infra or new thin wrapper
- Exact error-message copy for offline-fail and missing-.bat toast (follow existing ErrorMessageHelper patterns)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project planning
- `.planning/PROJECT.md` — Stack, constraints, v1.2 milestone scope, key decisions table
- `.planning/REQUIREMENTS.md` — WPLUS-01..04 requirement text and acceptance criteria
- `.planning/ROADMAP.md` §"Phase 8: WindrosePlus Bootstrap" — Goal + success criteria (4 items)
- `.planning/STATE.md` — Accumulated v1.0/v1.1 context, Nexus-API-removed constraint

### Existing code to integrate with
- `src/WindroseServerManager.Core/Services/ServerProcessService.cs` — current launcher; gains WindrosePlusService dependency
- `src/WindroseServerManager.Core/Services/ServerInstallService.cs` + `Models/ServerInstallInfo.cs` — per-server state where the "WindrosePlus active" flag lands
- `src/WindroseServerManager.Core/Services/SteamCmdService.cs` — reference pattern for `async Task ... IProgress<InstallProgress>` installer
- `src/WindroseServerManager.Core/Models/InstallProgress.cs` — reuse or extend for WindrosePlus install progress
- `src/WindroseServerManager.App/Services/UpdateCheckService.cs` — reference pattern for GitHub Releases API calls (app self-update uses the same API shape)
- `src/WindroseServerManager.App/Services/ErrorMessageHelper.cs` — consistent error-toast copy

### External
- `https://github.com/HumanGenome/WindrosePlus` — upstream repo; README + release assets are the install contract
- `https://docs.github.com/en/rest/releases/releases#get-the-latest-release` — GitHub Releases API endpoint for latest stable

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **SteamCmdService**: Template for long-running installer with `IProgress<InstallProgress>` + cancellation — WindrosePlusService mirrors this shape
- **UpdateCheckService**: Already calls GitHub Releases API for app self-update — same HttpClient + JSON shape can be reused for WindrosePlus releases
- **InstallProgress model**: Existing progress DTO; extend or reuse for WindrosePlus install phases (download, verify, extract, finalize)
- **ToastService / ErrorMessageHelper**: Standard UX for hard-fail offline message and missing-.bat warning
- **LocalizationService + Strings.de/en.xaml**: All user-facing strings (about section, errors, toasts) must go through this — bilingual from day one

### Established Patterns
- **Per-server state via ServerInstallInfo**: Existing per-server JSON/AppSettings container — natural home for the new `WindrosePlusActive` flag (+ later RCON password, dashboard port from Phase 9)
- **MIT LICENSE bundling**: Project already ships its own LICENSE; add WindrosePlus LICENSE as a sibling artifact in the install output
- **Chromeless FluentWindow / Avalonia 12**: No UI is built in Phase 8, but the About-dialog update must follow the existing dialog conventions

### Integration Points
- **ServerProcessService.StartAsync** path — asks WindrosePlusService for StartFilePath + args instead of hardcoding `WindroseServer.exe`
- **DI registration (App.axaml.cs / Program.cs)** — register `IWindrosePlusService` as Singleton alongside existing services
- **About dialog** — add "Third-Party Licenses" section and render WindrosePlus MIT text

</code_context>

<specifics>
## Specific Ideas

- Atomic install pattern: temp-extract → SHA verify → move into place. Mirrors how well-behaved installers avoid broken-server states.
- Version marker file is deliberately our own (`.wplus-version`) — decouples us from any future WindrosePlus version-exposure changes.
- Launcher-switch inconsistency (flag says on, .bat missing) is a health-check case, not a start-blocker — consistent with the v1.2 "never silently break a working server" principle.

</specifics>

<deferred>
## Deferred Ideas

- **Version pinning per server** (UPGRADE-01, v1.3) — user choice to lock a specific WindrosePlus version
- **Automatic background upgrade check** (UPGRADE-01, v1.3) — notify when upstream releases a new version
- **Prerelease channel opt-in** — advanced-user feature, not needed for v1.2
- **UE4SS version independence** — bundling our own UE4SS separately if HumanGenome ever drops it from their release (superseded by 2026-04-19 decision: we already fetch UE4SS independently via C# reimplementation of install.ps1)

</deferred>

---

*Phase: 08-windroseplus-bootstrap*
*Context gathered: 2026-04-19*
