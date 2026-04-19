# Milestones

## v1.0.0 — Stable Foundation (shipped 2026-04-19)

**Goal:** Ship a usable Windrose server manager covering install, control, config, mods, backups, and basic system integration.

**Phases:** Not tracked in GSD (pre-GSD era). Reconstructed from git history:

| # | Phase | Scope |
|---|-------|-------|
| 1 | Core shell | Avalonia chromeless window, navigation, theming, i18n scaffolding |
| 2 | Server install + control | SteamCMD wrapper, start/stop/restart, crash auto-restart, scheduled restarts |
| 3 | Configuration | JSON editor for ServerDescription / WorldDescription, world management |
| 4 | Backups + system | Backup scheduler, firewall, tray, autostart, update check |
| 5 | Mod management + Nexus | Drag-and-drop, grouping, Nexus metadata + update check, client bundle export |

**Released on:** Nexus Mods (#29), GitHub, Reddit announcement 2026-04-19
**Known issues post-release:**
- Nexus quarantined mod due to personal-API-key usage in public app
- Nexus "suspicious files" virus-scan FP (false positive for unsigned .NET binaries)

**Last phase number:** 5 → v1.1 starts at phase 6

## v1.1.0 — Nexus API Removal (shipped 2026-04-19)

**Goal:** Lift the Nexus Mods quarantine by removing all API usage, keeping mod management fully functional offline with URL-only "Open on Nexus" links.

| # | Phase | Scope |
|---|-------|-------|
| 6 | Nexus API Removal | Delete NexusClient, remove API key from Settings, drop update-check + thumbnails, keep "Open on Nexus" as pure URL launch |
| 7 | Release & Quarantine Lift | Rebuild, update docs, upload to Nexus mod #29, submit for moderation |

**Released on:** GitHub v1.1.0 and Nexus mod #29 (awaiting moderator approval for quarantine lift) 2026-04-19

**Last phase number:** 7 → v1.2 starts at phase 8

## v1.2.0 — WindrosePlus Integration (in planning)

**Goal:** Ship player-management, events, sea-chart, and config-editor features by bundling HumanGenome/WindrosePlus as the opt-in-by-default mod, with clean empty states when disabled.

**Phases:** TBD (roadmap in progress)
