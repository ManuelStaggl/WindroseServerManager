# State

## Current Position

Phase: 8 — WindrosePlus Bootstrap
Plan: 02 (next — Wave 1 implementation)
Status: Plan 08-01 complete — contract + fixtures + skipped behavior tests landed
Last activity: 2026-04-19 — Plan 08-01 executed (3 tasks, 55 passed / 13 skipped / 0 failed)

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-19)
See: .planning/ROADMAP.md (v1.2 — phases 8–12)
See: .planning/REQUIREMENTS.md (27 v1.2 requirements, fully mapped)

**Core value:** One-click, end-to-end management of a Windrose dedicated server on Windows.
**Current focus:** v1.2 — WindrosePlus Integration (player management, events, sea chart, config editor)

## Progress

| Phase | Status |
|-------|--------|
| 8. WindrosePlus Bootstrap | In progress — Plan 01/03 complete (contract + fixtures + skipped behavior tests) |
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

## Decisions (Plan 08-01)

- License filename bundled into server dir: `WindrosePlus-LICENSE.txt` (groups alphabetically with `.wplus-version` marker)
- Expected `WindrosePlusService` constructor signature for Plan 02: `(ILogger<WindrosePlusService> logger, IHttpClientFactory httpClientFactory, string cacheDir)`
- Per-server WP state keyed by full `InstallDir` path in `AppSettings.WindrosePlusActiveByServer` / `WindrosePlusVersionByServer`; `ServerInstallInfo` fields are read-through (no double-persistence)
- Wave-0 tests ship as `[Fact(Skip=...)]` against `IWindrosePlusService`; Plan 02 removes `Skip` args and instantiates the concrete class — minimal diff
- `FakeGithubReleaseServer.FailWindrosePlusAsset` toggle added to support the atomic-install-failure test (mid-download 500 simulation)

## Blockers

None.

## Next Step

Run `/gsd:execute-plan 8 2` to execute Plan 08-02 (Wave 1: `WindrosePlusService` implementation — unskip the 13 behavior tests).
