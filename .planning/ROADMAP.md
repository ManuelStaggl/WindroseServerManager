# Roadmap — v1.2 WindrosePlus Integration

**Milestone:** v1.2 — WindrosePlus Integration
**Defined:** 2026-04-19
**Phase range:** 8 → 12 (v1.1 ended at phase 7)
**Granularity:** Standard (5 phases for 27 requirements)
**Coverage:** 27/27 v1.2 requirements mapped

## Milestone Goal

Unlock player-management, events, sea-chart, and config-editor features by bundling HumanGenome/WindrosePlus (MIT) as the default-on, opt-out-capable mod — with transparent install flows, a shared service surface, and clean empty states when users opt out.

## Phases

- [x] **Phase 8: WindrosePlus Bootstrap** — Fetch, install, license-bundle, and launcher-switch infrastructure (shared `WindrosePlusService`)
 (completed 2026-04-19)
- [ ] **Phase 9: Opt-in UX (Wizard + Retrofit)** — New-server wizard step and retrofit dialog for v1.0/v1.1 servers
- [ ] **Phase 10: Health & Support** — Dashboard health-check banner + "Report to WindrosePlus" issue helper
- [ ] **Phase 11: Feature Views** — Players, Events, Sea-Chart, and INI/Multiplier editor built on the WindrosePlus HTTP/log surface
- [ ] **Phase 12: Empty States (Opt-out UX)** — Dedicated empty states with install CTAs in every WindrosePlus-dependent view

## Phase Details

### Phase 8: WindrosePlus Bootstrap
**Goal:** The app can download, cache, install, and launch WindrosePlus on any server the user owns — establishing the `WindrosePlusService` that every later phase consumes.
**Depends on:** Nothing (foundation)
**Requirements:** WPLUS-01, WPLUS-02, WPLUS-03, WPLUS-04
**Success Criteria** (what must be TRUE):
  1. The app fetches the latest WindrosePlus release from GitHub and stores the archive in a local cache; the cache is used as fallback when offline
  2. Running the install against a server produces a working UE4SS + WindrosePlus payload in the server's game-binaries directory and does not break the existing `WindroseServer.exe` launch path
  3. The WindrosePlus MIT LICENSE (HumanGenome) is present in the install output and visible in the app's About dialog; the product name "WindrosePlus" appears verbatim and is not rebranded
  4. When a server has WindrosePlus active, the launcher starts `StartWindrosePlusServer.bat`; when opted out, it starts `WindroseServer.exe` — the switch is automatic based on per-server state
**Plans:** 3/3 plans complete

Plans:
- [ ] 08-01-PLAN.md — Wave 0: contracts, models, test fixtures, failing test stubs
- [ ] 08-02-PLAN.md — Wave 1: WindrosePlusService implementation (GitHub fetch + atomic install + SHA verify + launcher resolution)
- [ ] 08-03-PLAN.md — Wave 2: DI wiring, ServerProcessService integration, About dialog Third-Party Licenses section, bilingual strings

### Phase 9: Opt-in UX (Wizard + Retrofit)
**Goal:** Users can enable WindrosePlus transparently — both during new-server creation and on existing servers carried over from v1.0/v1.1 — with one-click opt-out and a clear explanation of what they gain.
**Depends on:** Phase 8
**Requirements:** WIZARD-01, WIZARD-02, WIZARD-03, WIZARD-04, RETRO-01, RETRO-02, RETRO-03
**Success Criteria** (what must be TRUE):
  1. The new-server wizard has a WindrosePlus step that lists the features gained (Kick/Ban/Broadcast/Events/Chart/INI-Editor), links to the WindrosePlus GitHub, and is default-on with a one-click opt-out
  2. On wizard confirmation, the app generates a secure random RCON password, captures the admin Steam-ID, and picks a free local port for the WindrosePlus dashboard (no hardcoded port)
  3. First launch of v1.2 detects per existing server whether WindrosePlus is installed; servers without it are offered a non-modal retrofit dialog with the same feature list and opt-out
  4. The retrofit choice persists per server across restarts; no server is ever upgraded silently — explicit confirmation is required every time
**Plans:** 1/3 plans executed

Plans:
- [ ] 09-01-PLAN.md — Wave 1: foundation — OptInState enum, AppSettings + ServerInstallInfo extensions, RCON/SteamID/FreePort helpers, opt-in migration + Wave-0 xUnit tests
- [ ] 09-02-PLAN.md — Wave 2: InstallWizardWindow (3-step) + shared WindrosePlusOptInControl + InstallWizardViewModel + "New server" entry point + bilingual strings
- [ ] 09-03-PLAN.md — Wave 3: Retrofit banner on DashboardView + RetrofitDialog reusing WindrosePlusOptInControl (RETRO-02/03)

### Phase 10: Health & Support
**Goal:** When WindrosePlus is active but misbehaves (e.g. after a Windrose upstream update breaks the UE4SS hook), the user sees what's wrong and can report it upstream in one click.
**Depends on:** Phase 8
**Requirements:** HEALTH-01, HEALTH-02
**Success Criteria** (what must be TRUE):
  1. After server start, the app polls the WindrosePlus HTTP dashboard; if the endpoint does not respond within the health-check window, an inline banner appears in the server's main view explaining the incompatibility
  2. The banner's "Report to WindrosePlus" button opens a prefilled GitHub issue on the HumanGenome/WindrosePlus repository containing the Windrose version, WindrosePlus version, and server log tail
**Plans:** TBD

### Phase 11: Feature Views
**Goal:** The admin features that make v1.2 worth shipping — player management, event history, sea chart, and config editor — are all available and usable when WindrosePlus is active.
**Depends on:** Phase 8, Phase 10
**Requirements:** PLAYER-01, PLAYER-02, PLAYER-03, PLAYER-04, EVENT-01, EVENT-02, EVENT-03, CHART-01, CHART-02, EDITOR-01, EDITOR-02, EDITOR-03
**Success Criteria** (what must be TRUE):
  1. The Players view lists connected players (name, Steam-ID, alive state, session duration), refreshes on a configurable interval, and lets the user kick, ban (permanent or timed), and broadcast a chat message — each destructive action gated by a confirmation dialog
  2. The Events view streams join/leave records from `events.log` live via FileSystemWatcher, supports filtering/search by name/Steam-ID/type, and stays responsive at >1000 entries (virtualization or pagination)
  3. The Sea-Chart view renders a top-down world map with live player positions polled from WindrosePlus `/query`; clicking a marker opens a popover with name, Steam-ID, alive state, and ship info when available
  4. The INI/Multiplier editor lists all WindrosePlus-exposed settings grouped by category, validates values against the config schema inline before save, writes the config file on save, and prompts the user to restart when the server is running
**Plans:** TBD

### Phase 12: Empty States (Opt-out UX)
**Goal:** Users who opted out of WindrosePlus never see a broken or disabled feature — every WindrosePlus-dependent view explains what's missing and offers a one-click path to install it.
**Depends on:** Phase 9, Phase 11
**Requirements:** EMPTY-01, EMPTY-02
**Success Criteria** (what must be TRUE):
  1. When the current server has opted out of WindrosePlus, the Players, Events, Sea-Chart, and Editor views each render a dedicated empty state with an icon, a short explanation of the missing feature, and an "Install WindrosePlus" call-to-action
  2. Clicking the CTA triggers the retrofit flow from Phase 9 (RETRO-02) in-place, without requiring an app restart or navigation away from the current view
**Plans:** TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 8. WindrosePlus Bootstrap | 3/3 | Complete   | 2026-04-19 |
| 9. Opt-in UX (Wizard + Retrofit) | 1/3 | In Progress|  |
| 10. Health & Support | 0/0 | Not started | — |
| 11. Feature Views | 0/0 | Not started | — |
| 12. Empty States (Opt-out UX) | 0/0 | Not started | — |

## Coverage Map

| Requirement | Phase |
|-------------|-------|
| WPLUS-01 | 8 |
| WPLUS-02 | 8 |
| WPLUS-03 | 8 |
| WPLUS-04 | 8 |
| WIZARD-01 | 9 |
| WIZARD-02 | 9 |
| WIZARD-03 | 9 |
| WIZARD-04 | 9 |
| RETRO-01 | 9 |
| RETRO-02 | 9 |
| RETRO-03 | 9 |
| HEALTH-01 | 10 |
| HEALTH-02 | 10 |
| PLAYER-01 | 11 |
| PLAYER-02 | 11 |
| PLAYER-03 | 11 |
| PLAYER-04 | 11 |
| EVENT-01 | 11 |
| EVENT-02 | 11 |
| EVENT-03 | 11 |
| CHART-01 | 11 |
| CHART-02 | 11 |
| EDITOR-01 | 11 |
| EDITOR-02 | 11 |
| EDITOR-03 | 11 |
| EMPTY-01 | 12 |
| EMPTY-02 | 12 |

**Mapped:** 27/27 ✓ — no orphans, no duplicates.

## Dependency Graph

```
Phase 8 (Bootstrap)
  ├── Phase 9 (Wizard + Retrofit)
  ├── Phase 10 (Health)
  └── Phase 11 (Feature Views) ← also depends on Phase 10
                └── Phase 12 (Empty States) ← also depends on Phase 9
```

Phase 11 can start as soon as Phase 8 + 10 are done; Phase 9 can run in parallel with 10/11 if needed. Phase 12 is last because it consumes both the retrofit dialog (9) and the feature views (11).

## Notes

- Upstream coordination: GitHub issue posted at HumanGenome/WindrosePlus on 2026-04-19. User has chosen to proceed regardless of upstream response — MIT license covers bundling. No phase is blocked on HumanGenome.
- Nexus API removed in v1.1 — no Nexus API code is reintroduced in any v1.2 phase.

---
*Roadmap created: 2026-04-19*
