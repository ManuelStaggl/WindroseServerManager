# Requirements: Windrose Server Manager

**Defined:** 2026-04-19
**Milestone:** v1.1 — Nexus Compliance
**Core Value:** One-click, end-to-end management of a Windrose dedicated server on Windows.

## v1.1 Requirements

### Application Registration

- [ ] **APP-REG-01**: Windrose Server Manager is registered with Nexus and has received an application slug
- [ ] **APP-REG-02**: Registered application metadata (slug, name, logo, description) is archived in repo for future reference

### Authentication (SSO)

- [ ] **AUTH-01**: User can initiate Nexus sign-in via a "Sign in with Nexus" button in Settings
- [ ] **AUTH-02**: App opens the Nexus SSO page in the system browser with the correct callback target
- [ ] **AUTH-03**: App receives the resulting API key via loopback HTTP listener (or custom URL scheme) without manual paste
- [ ] **AUTH-04**: Received key is stored encrypted (Windows DPAPI) — never in plain text on disk
- [ ] **AUTH-05**: User can sign out, which revokes the local token and clears all Nexus state
- [ ] **AUTH-06**: Failed, timed-out, or cancelled auth shows a clear error with a retry path
- [ ] **AUTH-07**: All existing Nexus API calls in NexusClient use the SSO-obtained key transparently (no code duplication)

### Migration from v1.0

- [ ] **MIGR-01**: On first v1.1 launch, app detects existing personal API key in settings
- [ ] **MIGR-02**: User sees a one-time migration dialog explaining why and offering "Sign in with Nexus now" or "Later"
- [ ] **MIGR-03**: After successful SSO, the personal API key field is cleared from settings
- [ ] **MIGR-04**: Users who defer see a persistent warning banner in the Mods view until they migrate

### Release

- [ ] **REL-01**: v1.1 binary is built, signed (or unsigned with FP-disclaimer), and uploaded to Nexus mod #29
- [ ] **REL-02**: README + Nexus description are updated — no more "paste your API key" instructions, new SSO flow documented
- [ ] **REL-03**: Quarantine lift confirmed by Nexus moderators, mod visible to public again

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

## Out of Scope

| Feature | Reason |
|---------|--------|
| Refresh-token rotation | Only if Nexus SSO tokens expire; handle on demand |
| Web/remote UI | Native desktop positioning |
| Linux/macOS | Windrose server is Windows-only |
| Multi-server management | Single-server focus in v1.x |
| Custom mod hosting | Nexus is the ecosystem |
| Sea chart / multiplier editor / INI editor | v1.3+ |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| APP-REG-01 | Phase 6 | Pending |
| APP-REG-02 | Phase 6 | Pending |
| AUTH-01 | Phase 7 | Pending |
| AUTH-02 | Phase 7 | Pending |
| AUTH-03 | Phase 7 | Pending |
| AUTH-04 | Phase 7 | Pending |
| AUTH-05 | Phase 7 | Pending |
| AUTH-06 | Phase 7 | Pending |
| AUTH-07 | Phase 7 | Pending |
| MIGR-01 | Phase 8 | Pending |
| MIGR-02 | Phase 8 | Pending |
| MIGR-03 | Phase 8 | Pending |
| MIGR-04 | Phase 8 | Pending |
| REL-01 | Phase 9 | Pending |
| REL-02 | Phase 9 | Pending |
| REL-03 | Phase 9 | Pending |

**Coverage:**
- v1.1 requirements: 16 total
- Mapped to phases: 16/16 ✓

---
*Requirements defined: 2026-04-19*
*Last updated: 2026-04-19 after roadmap creation*
