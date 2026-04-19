# State

## Current Position

Phase: Not started (defining requirements)
Plan: —
Status: Milestone v1.2 started — defining requirements for WindrosePlus integration
Last activity: 2026-04-19 — v1.2 milestone initialized

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)
See: .planning/ROADMAP.md (pending v1.2 rewrite)

**Core value:** One-click, end-to-end management of a Windrose dedicated server on Windows.
**Current focus:** v1.2 — WindrosePlus Integration (player management, events, sea chart, config editor)

## Progress

| Phase | Status |
|-------|--------|
| TBD | Roadmap in progress |

## Accumulated Context

Carried over from v1.0 and v1.1:
- v1.0.0 shipped on Nexus/GitHub 2026-04-19 (5 phases)
- v1.1.0 shipped on GitHub 2026-04-19; Nexus mod #29 re-uploaded, awaiting moderator quarantine lift (phases 6–7)
- Nexus API removed entirely in v1.1 — "Open on Nexus" is URL-only; do NOT reintroduce API dependencies
- WindrosePlus collaboration proposal posted as GitHub Issue at HumanGenome/WindrosePlus 2026-04-19 — user chose to proceed independently on v1.2 regardless of upstream response (MIT license covers bundling)

v1.2 scope decisions:
- WindrosePlus is the core dependency — default-on with explicit opt-out, never silent
- Fetch-on-install via GitHub Releases API, cache locally for offline
- All WindrosePlus-dependent views (player list, events, sea chart, INI editor) must render clean empty states when opt-out is active, with CTA to install

## Blockers

None.

## Next Step

Define REQUIREMENTS.md for v1.2, then spawn roadmapper to create phases from Phase 8 onwards.
