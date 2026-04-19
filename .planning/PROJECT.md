# Windrose Server Manager

## What This Is

Native Windows desktop app (Avalonia / .NET 9) for managing Windrose Dedicated Servers. Bundles SteamCMD, server control, configuration, mod management, backups, firewall rules, and update checks into a clean, modern UI. Targets self-hosters running the Windrose dedicated server (Steam App 4129620) on their own machine or VPS — alternative to renting from G-Portal/Nitrado.

## Core Value

One-click, end-to-end management of a Windrose dedicated server on Windows — from install to daily operations — without the user ever touching a config file or shell.

## Requirements

### Validated

<!-- Shipped in v1.0.0, confirmed valuable via Nexus/Reddit release 2026-04-19. -->

- ✓ SteamCMD-based server installation with live progress — v1.0
- ✓ Server start/stop/restart + auto-restart on crash — v1.0
- ✓ Scheduled restarts (per weekday, threshold-based) — v1.0
- ✓ Live log viewer with color coding — v1.0
- ✓ Form-based editor for ServerDescription.json and WorldDescription.json — v1.0
- ✓ Multi-world management (create, activate, delete) — v1.0
- ✓ Mod installation via drag-and-drop (pak/zip/7z), grouping, enable/disable — v1.0
- ✓ Nexus Mods integration (metadata, update check, auto-linking from filename) — v1.0
- ✓ Mod bundle export (ZIP of active mods for clients) — v1.0
- ✓ Backup system (manual + scheduled, retention, safe restore) — v1.0
- ✓ Firewall rule automation (UDP 7777/7778) — v1.0
- ✓ Tray icon + autostart — v1.0
- ✓ App self-update check via GitHub releases — v1.0
- ✓ Bilingual UI (DE/EN) with auto-detection — v1.0
- ✓ Dashboard with host metrics, process metrics, uptime, invite code — v1.0

### Active

<!-- v1.1 — Nexus Compliance (blocking). -->

## Current Milestone: v1.1 Nexus Compliance

**Goal:** Unblock Nexus distribution by migrating from user-supplied personal API keys to a registered-application SSO flow, as required by the Nexus API Acceptable Use Policy.

**Target features:**
- Register Windrose Server Manager as an official Nexus application (email sent 2026-04-19, slug pending)
- Replace manual API-key entry with SSO flow (nexusmods.com SSO → deep-link / loopback callback → token)
- Clean migration path for v1.0 users (existing personal keys keep working OR prompted re-auth via SSO)
- Rebuild + resubmit release to Nexus for quarantine lift

**Explicitly deferred to v1.2:** Player management + WindrosePlus integration (blocked on upstream response, not on our side)

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
| WindrosePlus as player-management dependency (v1.1 plan) | Only viable path to kick/ban/broadcast without reimplementing UE5 hooks | — Pending upstream response |
| Fetch-on-install for WindrosePlus via GitHub Releases API | Always latest, no bundled snapshot to maintain | — Pending |
| DE/EN only, DE as default | Core audience is DACH; EN needed for international reach | ✓ Good |

---
*Last updated: 2026-04-19 after v1.0 release and GSD bootstrap for v1.1*
