# Roadmap — v1.1 Nexus Compliance

**Milestone:** v1.1 — Nexus Compliance
**Defined:** 2026-04-19
**Phase range:** 6 → 9 (v1.0 ended at phase 5)
**Coverage:** 16/16 v1.1 requirements mapped

## Phases

- [ ] **Phase 6: Application Registration** — Register Windrose Server Manager as an official Nexus application and archive returned metadata (blocks on Nexus response)
- [ ] **Phase 7: Nexus SSO Authentication** — Replace personal-API-key entry with a browser-based Sign-in-with-Nexus flow, end-to-end
- [ ] **Phase 8: Migration from v1.0** — Detect legacy personal keys and guide existing users onto SSO without breaking their setup
- [ ] **Phase 9: Release & Quarantine Lift** — Rebuild, resubmit to Nexus mod #29, update documentation, and get the mod un-quarantined

## Phase Details

### Phase 6: Application Registration
**Goal:** Nexus has accepted Windrose Server Manager as a registered application and issued a stable slug that the SSO flow can target.
**Depends on:** Nexus moderation response (external — email sent 2026-04-19)
**Requirements:** APP-REG-01, APP-REG-02
**Success Criteria** (what must be TRUE):
  1. An application slug issued by Nexus exists and can be used in an SSO URL
  2. The full registered metadata set (slug, display name, logo asset, description) is committed to the repo so future maintainers can find it
  3. Ops status of the registration (submitted / approved / rejected) is visible in the project state
**Plans:** TBD
**Notes:** This phase has a waiting state — Phase 7 is blocked until the slug arrives.

### Phase 7: Nexus SSO Authentication
**Goal:** A user on a fresh install can sign in to Nexus from inside the app, without ever seeing or pasting an API key.
**Depends on:** Phase 6 (requires issued slug)
**Requirements:** AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTH-07
**Success Criteria** (what must be TRUE):
  1. User can click "Sign in with Nexus" in Settings, complete the flow in their system browser, and return to the app signed in — no manual paste
  2. Signed-in user can make every existing Nexus call (mod metadata, update check, image download) with no behavioural change vs. v1.0
  3. User can sign out and, after sign-out, no Nexus token remains on disk and Nexus-dependent features degrade gracefully
  4. On cancelled, timed-out, or rejected auth, the user sees a clear error and a retry button — never a silent hang
  5. Stored token on disk is encrypted (DPAPI) and cannot be read as plain text
**Plans:** TBD

### Phase 8: Migration from v1.0
**Goal:** Existing v1.0 users upgrading to v1.1 are guided to SSO without losing Nexus functionality and without being forced to act on launch.
**Depends on:** Phase 7
**Requirements:** MIGR-01, MIGR-02, MIGR-03, MIGR-04
**Success Criteria** (what must be TRUE):
  1. On first v1.1 launch of an installation that had a v1.0 personal API key, the user sees a one-time dialog explaining the change and offering "Sign in with Nexus now" or "Later"
  2. Completing SSO from the migration dialog removes the legacy personal-key field from settings
  3. A user who chooses "Later" sees a persistent, dismissable-per-session warning banner in the Mods view until they migrate
  4. A user without any stored legacy key never sees the migration dialog or banner
**Plans:** TBD

### Phase 9: Release & Quarantine Lift
**Goal:** v1.1 is publicly downloadable from Nexus mod #29, the quarantine is lifted, and documentation reflects the new auth model.
**Depends on:** Phase 8
**Requirements:** REL-01, REL-02, REL-03
**Success Criteria** (what must be TRUE):
  1. A v1.1 binary is built, packaged, and uploaded to Nexus mod #29 with updated changelog
  2. README on GitHub and description on Nexus describe the SSO flow and no longer instruct users to paste a personal API key
  3. Nexus moderators confirm the quarantine is lifted and the mod page is publicly visible and downloadable again
**Plans:** TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 6. Application Registration | 0/0 | Not started (waiting on Nexus) | — |
| 7. Nexus SSO Authentication | 0/0 | Not started | — |
| 8. Migration from v1.0 | 0/0 | Not started | — |
| 9. Release & Quarantine Lift | 0/0 | Not started | — |

## Coverage Map

| Requirement | Phase |
|-------------|-------|
| APP-REG-01 | 6 |
| APP-REG-02 | 6 |
| AUTH-01 | 7 |
| AUTH-02 | 7 |
| AUTH-03 | 7 |
| AUTH-04 | 7 |
| AUTH-05 | 7 |
| AUTH-06 | 7 |
| AUTH-07 | 7 |
| MIGR-01 | 8 |
| MIGR-02 | 8 |
| MIGR-03 | 8 |
| MIGR-04 | 8 |
| REL-01 | 9 |
| REL-02 | 9 |
| REL-03 | 9 |

**Mapped:** 16/16 ✓ — no orphans, no duplicates.

---
*Roadmap created: 2026-04-19*
