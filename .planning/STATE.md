# State

## Current Position

Phase: 8 — WindrosePlus Bootstrap
Plan: —
Status: Roadmap complete — ready for `/gsd:plan-phase 8`
Last activity: 2026-04-19 — v1.2 roadmap created (phases 8–12)

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)
See: .planning/ROADMAP.md (v1.2 — phases 8–12)
See: .planning/REQUIREMENTS.md (27 v1.2 requirements, fully mapped)

**Core value:** One-click, end-to-end management of a Windrose dedicated server on Windows.
**Current focus:** v1.2 — WindrosePlus Integration (player management, events, sea chart, config editor)

## Progress

| Phase | Status |
|-------|--------|
| 8. WindrosePlus Bootstrap | Not started — next |
| 9. Opt-in UX (Wizard + Retrofit) | Not started |
| 10. Health & Support | Not started |
| 11. Feature Views | Not started |
| 12. Empty States (Opt-out UX) | Not started |

## Accumulated Context

Carried over from v1.0 and v1.1:
- v1.0.0 shipped on Nexus/GitHub 2026-04-19 (phases 1–5)
- v1.1.0 shipped on GitHub 2026-04-19; Nexus mod #29 re-uploaded, awaiting moderator quarantine lift (phases 6–7)
- Nexus API removed entirely in v1.1 — "Open on Nexus" is URL-only; do NOT reintroduce API dependencies

v1.2 scope decisions:
- WindrosePlus is the core dependency — default-on with explicit opt-out, never silent
- Fetch-on-install via GitHub Releases API, cache locally for offline
- All WindrosePlus-dependent views (player list, events, sea chart, INI editor) must render clean empty states when opt-out is active, with CTA to install
- Phase 8 establishes a shared `WindrosePlusService` consumed by all later phases
- WindrosePlus collaboration proposal posted as GitHub issue at HumanGenome/WindrosePlus 2026-04-19 — user chose to proceed independently regardless of upstream response (MIT license covers bundling)

## Blockers

None.

## Next Step

Run `/gsd:plan-phase 8` to break Phase 8 (WindrosePlus Bootstrap) into executable plans.
