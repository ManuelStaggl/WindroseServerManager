# Requirements: Windrose Server Manager

**Defined:** 2026-04-19
**Last updated:** 2026-04-19 — v1.2 milestone scoped, roadmap mapped
**Milestone:** v1.2 — WindrosePlus Integration
**Core Value:** One-click, end-to-end management of a Windrose dedicated server on Windows.

## v1.2 Scope Rationale

Windrose has no native admin features (no RCON, no A2S response, no admin console). HumanGenome/WindrosePlus (MIT, UE4SS-based) is the only path to kick/ban/broadcast, events, player positions, and config tuning. v1.2 bundles WindrosePlus as a default-on, opt-out-capable dependency and builds the native Windows client UX around it. Fetch-on-install via GitHub Releases API keeps us off a bundled-snapshot maintenance treadmill.

## v1.2 Requirements

### Bootstrap (WindrosePlus packaging)

- [ ] **WPLUS-01**: App fetches the latest WindrosePlus release from GitHub Releases API and caches the archive locally as offline-install fallback
- [ ] **WPLUS-02**: WindrosePlus install extracts into the active server's game-binaries directory and produces a working UE4SS + WindrosePlus payload
- [ ] **WPLUS-03**: WindrosePlus LICENSE (MIT, HumanGenome) is bundled with the install and shown in the About dialog; name "WindrosePlus" is never rebranded
- [ ] **WPLUS-04**: On server launch, app uses `StartWindrosePlusServer.bat` when WindrosePlus is active and `WindroseServer.exe` when the server has opted out

### Install Wizard

- [ ] **WIZARD-01**: New-server wizard includes a WindrosePlus step listing features gained (Kick/Ban/Broadcast/Events/Chart/INI-Editor) with a link to the WindrosePlus GitHub
- [ ] **WIZARD-02**: WindrosePlus is default-on in the wizard; user can opt-out with one click
- [ ] **WIZARD-03**: Wizard sets a secure random RCON password and captures the admin Steam-ID on confirmation
- [ ] **WIZARD-04**: Wizard picks a free local port for the WindrosePlus dashboard (no fixed port, supports multi-instance users)

### Retrofit (existing servers from v1.0/v1.1)

- [ ] **RETRO-01**: First launch after upgrading to v1.2 detects per server whether WindrosePlus is installed
- [ ] **RETRO-02**: For servers without WindrosePlus, a non-modal dialog offers installation with feature list + opt-out; the choice persists per server
- [ ] **RETRO-03**: Retrofit never installs silently; user must explicitly confirm

### Health & Support

- [ ] **HEALTH-01**: App polls the WindrosePlus HTTP dashboard after server start and shows an inline banner if the endpoint does not respond
- [ ] **HEALTH-02**: The incompat banner offers a "Report to WindrosePlus" button that opens a prefilled GitHub issue (Windrose version, WindrosePlus version, server log tail)

### Player Management

- [ ] **PLAYER-01**: Players view lists all currently connected players with name, Steam-ID, alive state, session duration; refreshes on a configurable interval
- [ ] **PLAYER-02**: User can kick a selected player with a confirmation dialog
- [ ] **PLAYER-03**: User can ban a selected player (permanent or timed) with a confirmation dialog
- [ ] **PLAYER-04**: User can broadcast a chat message to all connected players via a single input field

### Events History

- [ ] **EVENT-01**: Events view streams join/leave records from WindrosePlus `events.log` live (FileSystemWatcher)
- [ ] **EVENT-02**: Events can be searched and filtered by player name, Steam-ID, and event type
- [ ] **EVENT-03**: Events view paginates or virtualizes for >1000 entries (no UI freeze)

### Sea-Chart Viewer

- [ ] **CHART-01**: Sea-chart view renders a top-down world map with live player positions polled from WindrosePlus `/query`
- [ ] **CHART-02**: Clicking a player marker opens a popover with name, Steam-ID, alive state, ship info (if available)

### Multiplier / INI Editor

- [ ] **EDITOR-01**: Editor lists all WindrosePlus-exposed settings (spawn multipliers, loot rates, etc.) grouped by category
- [ ] **EDITOR-02**: Editor validates values against the WindrosePlus config schema before save; invalid values show inline errors
- [ ] **EDITOR-03**: Save writes the WindrosePlus config file and, if the server is running, notifies the user that a restart is needed

### Empty States (opt-out UX)

- [ ] **EMPTY-01**: Players / Events / Chart / Editor views each render a dedicated empty state with explanation + "Install WindrosePlus" CTA when the current server has opted out
- [ ] **EMPTY-02**: Empty-state CTA triggers the retrofit flow (RETRO-02) without requiring a restart

## Future Requirements (v1.3+)

- **UPGRADE-01**: Background check for new WindrosePlus upstream releases with user-confirmed upgrade
- **MULTI-01**: Multi-server management in a single window

## Out of Scope (v1.2)

| Feature | Reason |
|---------|--------|
| Automatic WindrosePlus version bumps | User-triggered only in v1.2; background upgrade UX is v1.3 |
| SteamCMD-based WindrosePlus install | Upstream distributes via GitHub Releases only |
| Rebranding WindrosePlus in UI | MIT attribution preserves upstream name |
| Reporting WindrosePlus bugs through our GitHub | Routed directly to HumanGenome via HEALTH-02 button |
| Linux/macOS support | Windrose server is Windows-only |
| Web/remote UI | Native desktop positioning |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| WPLUS-01 | 8 | Pending |
| WPLUS-02 | 8 | Pending |
| WPLUS-03 | 8 | Pending |
| WPLUS-04 | 8 | Pending |
| WIZARD-01 | 9 | Pending |
| WIZARD-02 | 9 | Pending |
| WIZARD-03 | 9 | Pending |
| WIZARD-04 | 9 | Pending |
| RETRO-01 | 9 | Pending |
| RETRO-02 | 9 | Pending |
| RETRO-03 | 9 | Pending |
| HEALTH-01 | 10 | Pending |
| HEALTH-02 | 10 | Pending |
| PLAYER-01 | 11 | Pending |
| PLAYER-02 | 11 | Pending |
| PLAYER-03 | 11 | Pending |
| PLAYER-04 | 11 | Pending |
| EVENT-01 | 11 | Pending |
| EVENT-02 | 11 | Pending |
| EVENT-03 | 11 | Pending |
| CHART-01 | 11 | Pending |
| CHART-02 | 11 | Pending |
| EDITOR-01 | 11 | Pending |
| EDITOR-02 | 11 | Pending |
| EDITOR-03 | 11 | Pending |
| EMPTY-01 | 12 | Pending |
| EMPTY-02 | 12 | Pending |

**Coverage:**
- v1.2 requirements: 27 total
- Mapped to phases: 27/27 ✓ — no orphans, no duplicates

---
*Requirements defined: 2026-04-19*
*Last updated: 2026-04-19 — v1.2 WindrosePlus Integration, mapped to phases 8–12*
