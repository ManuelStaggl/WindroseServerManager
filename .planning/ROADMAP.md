# Roadmap — v1.1 Nexus API Removal

**Milestone:** v1.1 — Nexus API Removal
**Defined:** 2026-04-19 (original SSO plan)
**Rescoped:** 2026-04-19 — pivot from SSO integration to full API removal
**Phase range:** 6 → 7 (v1.0 ended at phase 5)
**Coverage:** 13/13 v1.1 requirements mapped

## Rationale for the Pivot

The original v1.1 plan migrated mod management to Nexus SSO. After reflection, that adds auth code, token storage, registration overhead, and migration UX — all for a feature (live metadata + update checks) that is convenience, not core value. Removing the Nexus API entirely lifts the quarantine cause, shrinks the codebase, and keeps mod management fully functional. "Open on Nexus" continues to work as a pure URL launch.

## Phases

- [ ] **Phase 6: Nexus API Removal** — Delete NexusClient and all API surface; keep "Open on Nexus" via URL construction; clean up Settings and Mods UI
- [ ] **Phase 7: Release & Quarantine Lift** — Rebuild, update docs, resubmit to Nexus mod #29, get the mod un-quarantined

## Phase Details

### Phase 6: Nexus API Removal
**Goal:** The codebase contains no Nexus API client, no API key storage, and no update-check machinery — yet the Mods feature still links to each mod's Nexus page and all existing installed mods keep working.
**Depends on:** Nothing external
**Requirements:** API-RM-01 … API-RM-10
**Success Criteria** (what must be TRUE):
  1. Project builds and tests pass with zero references to `NexusClient`, `NexusModInfo`, or an API key
  2. Settings page has no API-key field; AppSettings model has no API-key property
  3. Each mod card shows an "Open on Nexus" button that launches the system browser to the mod's Nexus page when a `NexusModId` is known
  4. Starting v1.1 with a v1.0 settings file containing an API key does not crash; the key is removed silently from persisted settings on next save
  5. `NexusUrlParser` and `NexusModId` metadata on mods are preserved
  6. No "update available" indicator or thumbnail placeholder appears anywhere in the Mods view
**Plans:** TBD

### Phase 7: Release & Quarantine Lift
**Goal:** v1.1 is publicly downloadable from Nexus mod #29, the quarantine is lifted, and all documentation reflects the API-free workflow.
**Depends on:** Phase 6
**Requirements:** REL-01, REL-02, REL-03
**Success Criteria** (what must be TRUE):
  1. A v1.1 binary is built, packaged, and uploaded to Nexus mod #29 with an updated changelog that explains the API removal
  2. README on GitHub and description on Nexus describe the "Open on Nexus" workflow and contain no mention of API keys or in-app update checks
  3. Nexus moderators confirm the quarantine is lifted and the mod page is publicly visible and downloadable again
**Plans:** TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 6. Nexus API Removal | 0/0 | Not started | — |
| 7. Release & Quarantine Lift | 0/0 | Not started | — |

## Coverage Map

| Requirement | Phase |
|-------------|-------|
| API-RM-01 | 6 |
| API-RM-02 | 6 |
| API-RM-03 | 6 |
| API-RM-04 | 6 |
| API-RM-05 | 6 |
| API-RM-06 | 6 |
| API-RM-07 | 6 |
| API-RM-08 | 6 |
| API-RM-09 | 6 |
| API-RM-10 | 6 |
| REL-01 | 7 |
| REL-02 | 7 |
| REL-03 | 7 |

**Mapped:** 13/13 ✓ — no orphans, no duplicates.

## Dropped from v1.1 (was in earlier draft)

The following phases and requirements were part of the original SSO-migration plan and are **explicitly out of scope** after the pivot:

- ~~Phase 6: Application Registration (Nexus app slug)~~ — no registration needed
- ~~Phase 7: Nexus SSO Authentication~~ — no auth needed
- ~~Phase 8: Migration from v1.0 (SSO migration dialog)~~ — replaced by silent key clear (API-RM-09)
- ~~APP-REG-01/02, AUTH-01 … AUTH-07, MIGR-01 … MIGR-04~~ — no longer applicable

---
*Roadmap created: 2026-04-19*
*Rescoped: 2026-04-19 — v1.1 now targets API removal, not SSO migration*
