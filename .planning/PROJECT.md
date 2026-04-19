# Windrose Server Manager

## What This Is

Native Windows desktop app (Avalonia / .NET 9) for managing Windrose Dedicated Servers. Bundles SteamCMD, server control, configuration, mod management, backups, firewall rules, and update checks into a clean, modern UI. Targets self-hosters running the Windrose dedicated server (Steam App 4129620) on their own machine or VPS — alternative to renting from G-Portal/Nitrado.

## Core Value

One-click, end-to-end management of a Windrose dedicated server on Windows — from install to daily operations — without the user ever touching a config file or shell.

## Requirements

### Validated

<!-- Shipped in v1.0.0 and v1.1.0. -->

- ✓ SteamCMD-based server installation with live progress — v1.0
- ✓ Server start/stop/restart + auto-restart on crash — v1.0
- ✓ Scheduled restarts (per weekday, threshold-based) — v1.0
- ✓ Live log viewer with color coding — v1.0
- ✓ Form-based editor for ServerDescription.json and WorldDescription.json — v1.0
- ✓ Multi-world management (create, activate, delete) — v1.0
- ✓ Mod installation via drag-and-drop (pak/zip/7z), grouping, enable/disable — v1.0
- ✓ Mod bundle export (ZIP of active mods for clients) — v1.0
- ✓ Backup system (manual + scheduled, retention, safe restore) — v1.0
- ✓ Firewall rule automation (UDP 7777/7778) — v1.0
- ✓ Tray icon + autostart — v1.0
- ✓ App self-update check via GitHub releases — v1.0
- ✓ Bilingual UI (DE/EN) with auto-detection — v1.0
- ✓ Dashboard with host metrics, process metrics, uptime, invite code — v1.0
- ✓ Nexus API-free mod integration — "Open on Nexus" via URL construction only — v1.1

### Active

<!-- v1.2 — WindrosePlus Integration. -->

## Current Milestone: v1.2 WindrosePlus Integration

**Goal:** Unlock player-management, events, sea-chart, and config-editor features by bundling HumanGenome/WindrosePlus (MIT) as the default-on mod, with transparent opt-out and clean empty states.

**Target features:**
- WindrosePlus fetch-on-install via GitHub Releases API + local cache fallback + MIT compliance
- Install wizard step with opt-out, explicit feature list, link to WindrosePlus source
- Retrofit dialog for existing servers from v1.0/v1.1 (no silent install)
- Health-check banner + "Report to WindrosePlus" GitHub-issue helper
- Player management UI (live list, kick, ban, broadcast) via WindrosePlus HTTP API
- Events history (join/leave) from `events.log` with FileSystemWatcher, searchable
- Sea-chart viewer showing live player positions from WindrosePlus `/query`
- Multiplier / INI editor for WindrosePlus config
- Launcher switch to `StartWindrosePlusServer.bat` when WindrosePlus is active (PAK auto-rebuild)
- Clean empty states in every WindrosePlus-dependent view when opt-out is active

### Out of Scope

- **Linux/macOS support** — Windrose Dedicated Server is Windows-only; no value in porting the manager
- **Multi-server management in one UI** — deferred; single-server focus reduces scope significantly in v1.x
- **Web/remote UI** — native desktop is the positioning; web would be a separate product
- **Custom mod hosting** — Nexus is the ecosystem, no reason to fragment

## Context

- **Published on Nexus Mods** (mod #29) and Reddit, v1.0.0 released 2026-04-19
- **Open source, MIT license** — github.com/ManuelStaggl/WindroseServerManager
- **Windrose is in Early Access** — API/log-format changes possible, tolerance for churn required
- **No native admin features in Windrose**: no RCON, no A2S query response, no admin console. Third-party mod WindrosePlus (github.com/HumanGenome/WindrosePlus, MIT, UE4SS-based) is currently the only path to player-management features
- **Nexus quarantine incident (2026-04-19)**: Current Nexus integration uses user-supplied personal API keys, which is prohibited for public apps. Application registration + SSO flow required for continued distribution — blocking for v1.1

## Constraints

- **Tech stack**: .NET 9, Avalonia 12, CommunityToolkit.Mvvm — chromeless FluentWindow pattern — no reversal planned
- **Platform**: Windows 10/11 only — Windrose server is Windows-only
- **License**: MIT — any bundled third-party component must be compatible (MIT/Apache/BSD acceptable)
- **Dependency policy**: Pin exact versions, verify via Context7 MCP, no floating versions, no preview packages unless explicitly requested
- **UI**: Doppelmayr brand guidelines do NOT apply — this is a community project, not internal tooling. Design is independent amber/dark-mode-first palette

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Avalonia instead of WPF | Cross-platform UI framework with better modern Fluent styling, though we only ship Windows builds | ✓ Good — smooth UX, Mica works |
| Nexus integration via user API key (v1.0) | Fastest path to shipping mod metadata/updates | ⚠️ Revisit — Nexus quarantined the mod, must migrate to registered-app SSO for v1.1 |
| Nexus API removed (v1.1 pivot) | SSO migration cost outweighed the convenience-only feature | ✓ Good — quarantine cause eliminated |
| WindrosePlus as player-management dependency (v1.2) | Only viable path to kick/ban/broadcast/position queries without reimplementing UE5 hooks; MIT license allows bundling | — Pending v1.2 delivery |
| Fetch-on-install for WindrosePlus via GitHub Releases API | Always latest, no bundled snapshot to maintain, parallels existing Nexus-style flow | — Pending v1.2 delivery |
| WindrosePlus default-on with explicit opt-out | DLL-injection must be transparent, not silent; feature set collapses to empty states when disabled | — Pending v1.2 delivery |
| DE/EN only, DE as default | Core audience is DACH; EN needed for international reach | ✓ Good |

---
*Last updated: 2026-04-19 after v1.1.0 release — milestone v1.2 WindrosePlus Integration started*
