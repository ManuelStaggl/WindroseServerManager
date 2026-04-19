# Requirements: Windrose Server Manager

**Defined:** 2026-04-19
**Last updated:** 2026-04-19 — v1.1 scope pivot (API removal instead of SSO)
**Milestone:** v1.1 — Nexus API Removal
**Core Value:** One-click, end-to-end management of a Windrose dedicated server on Windows.

## v1.1 Scope Rationale

v1.0 used personal Nexus API keys in the Mods feature, which violated Nexus's policy for third-party tools and caused mod #29 to be quarantined. Rather than migrating to Nexus SSO (which requires a formal app registration and introduces auth/token/migration complexity for a convenience-only feature), v1.1 **removes the Nexus API integration entirely**. Mod management continues to work fully offline; Nexus is referenced only via direct page links that the user can click.

## v1.1 Requirements

### Nexus API Removal

- [ ] **API-RM-01**: `NexusClient` and all Nexus HTTP/API calls are removed from the codebase
- [ ] **API-RM-02**: `NexusModInfo` and any API-response models are removed
- [ ] **API-RM-03**: Personal API key field is removed from Settings UI and `AppSettings` model
- [ ] **API-RM-04**: Update-check logic (compare local vs Nexus latest) is removed from `ModService` and all ViewModels — no "update available" badges remain
- [ ] **API-RM-05**: Thumbnail download/display for mods is removed; UI falls back to a generic mod icon
- [ ] **API-RM-06**: "Open on Nexus" button on each mod constructs `https://www.nexusmods.com/windrose/mods/{NexusModId}` and launches the system browser — no API call
- [ ] **API-RM-07**: `LinkNexusDialog` is simplified to a ModId input only (no API fetch, no preview) — or removed if ModId is captured during mod-add instead
- [ ] **API-RM-08**: `NexusUrlParser` is retained (static URL → ModId parsing, no network)
- [ ] **API-RM-09**: On first v1.1 launch, any previously stored API key in settings is silently cleared from disk — no user-facing migration dialog
- [ ] **API-RM-10**: README and in-app help text no longer mention API keys, Nexus account linking, or update checks

### Release

- [ ] **REL-01**: v1.1 binary is built and uploaded to Nexus mod #29 with an updated changelog
- [ ] **REL-02**: README on GitHub and description on Nexus are updated — no API-key instructions, no update-check claims, new "Open on Nexus" workflow documented
- [ ] **REL-03**: Nexus moderators confirm the quarantine is lifted and mod #29 is publicly visible and downloadable again

## v1.2 Requirements (deferred)

### Player Management (blocked on HumanGenome/WindrosePlus response)

- **PLAYER-01**: WindrosePlus is offered as default-on in the server install wizard with clear opt-out
- **PLAYER-02**: Fetch-on-install via GitHub Releases API with local cache fallback
- **PLAYER-03**: Retrofit dialog detects v1.0 servers without WindrosePlus and offers installation
- **PLAYER-04**: Live player list with name, Steam-ID, alive state, online time
- **PLAYER-05**: Kick / Ban / Broadcast actions via WindrosePlus HTTP commands
- **PLAYER-06**: Events history (join/leave) from `events.log` with FileSystemWatcher
- **PLAYER-07**: Clean empty states for all WindrosePlus-dependent views when opt-out is active
- **PLAYER-08**: Health-check banner when WindrosePlus is incompatible with current Windrose version
- **PLAYER-09**: "Report to WindrosePlus" button routes bugs upstream with prefilled GitHub issue template

## Out of Scope (v1.1)

| Feature | Reason |
|---------|--------|
| Nexus SSO integration | API removed entirely — no auth needed |
| App registration with Nexus | Not required without API usage |
| Automatic mod update checks | Requires API — explicitly removed; user checks Nexus pages manually |
| Mod thumbnails / metadata in-app | Requires API — explicitly removed |
| Web/remote UI | Native desktop positioning |
| Linux/macOS | Windrose server is Windows-only |
| Multi-server management | Single-server focus in v1.x |
| Custom mod hosting | Nexus remains the ecosystem; we just link to it |
| Sea chart / multiplier editor / INI editor | v1.3+ |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| API-RM-01 | Phase 6 | Pending |
| API-RM-02 | Phase 6 | Pending |
| API-RM-03 | Phase 6 | Pending |
| API-RM-04 | Phase 6 | Pending |
| API-RM-05 | Phase 6 | Pending |
| API-RM-06 | Phase 6 | Pending |
| API-RM-07 | Phase 6 | Pending |
| API-RM-08 | Phase 6 | Pending |
| API-RM-09 | Phase 6 | Pending |
| API-RM-10 | Phase 6 | Pending |
| REL-01 | Phase 7 | Pending |
| REL-02 | Phase 7 | Pending |
| REL-03 | Phase 7 | Pending |

**Coverage:**
- v1.1 requirements: 13 total
- Mapped to phases: 13/13 ✓

---
*Requirements defined: 2026-04-19*
*Last updated: 2026-04-19 — scope pivot from SSO to API removal*
