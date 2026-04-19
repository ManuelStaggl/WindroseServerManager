# State

## Current Position

Phase: 6 — Nexus API Removal (next)
Plan: —
Status: v1.1 milestone rescoped on 2026-04-19 — pivot from SSO migration to full Nexus API removal. Phase 6 has no external blocker and is ready to plan.
Last activity: 2026-04-19 — REQUIREMENTS.md and ROADMAP.md rewritten for the API-removal scope

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)
See: .planning/ROADMAP.md (rescoped 2026-04-19)

**Core value:** One-click, end-to-end management of a Windrose dedicated server on Windows.
**Current focus:** v1.1 — Nexus API Removal (quarantine lift by removing the API, not by migrating to SSO)

## Progress

| Phase | Status |
|-------|--------|
| 6. Nexus API Removal | Not started — ready to plan |
| 7. Release & Quarantine Lift | Not started — blocked by Phase 6 |

## Accumulated Context

Carried over from v1.0:
- App is live on Nexus/GitHub since 2026-04-19
- Nexus mod #29 currently quarantined (personal-API-key policy violation)
- WindrosePlus collaboration proposal posted as GitHub Issue at HumanGenome/WindrosePlus 2026-04-19 (v1.2 concern, not blocking v1.1)

v1.1 scope decisions:
- SSO integration was evaluated and rejected — too much auth complexity for a convenience feature
- Removing the Nexus API entirely eliminates the compliance violation without auth code
- "Open on Nexus" button continues to work via pure URL construction (`https://www.nexusmods.com/windrose/mods/{id}`) — no network call
- Nexus moderation email (sent 2026-04-19 for app registration) is now obsolete — no follow-up needed

## Blockers

None.

## Next Step

Run `/gsd:plan-phase 6` to plan the Nexus API removal.
