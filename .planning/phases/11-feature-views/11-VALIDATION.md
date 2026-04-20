---
phase: 11
slug: feature-views
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-20
updated: 2026-04-20
---

# Phase 11 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (.NET) |
| **Config file** | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| **Quick run command** | `dotnet test tests/WindroseServerManager.Core.Tests/ --no-build -q` |
| **Full suite command** | `dotnet test --no-build -q` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/WindroseServerManager.Core.Tests/ --no-build -q`
- **After every plan wave:** Run `dotnet test --no-build -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 11-01-01 | 01 | 1 | PLAYER-01..04 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.WindrosePlusApiServiceTests"` | ❌ W0 | ⬜ pending |
| 11-01-01 | 01 | 1 | PLAYER-02,PLAYER-03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.WindrosePlusApiServiceTests"` | ❌ W0 | ⬜ pending |
| 11-01-01 | 01 | 1 | EVENT-01..03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.EventsLogParserTests"` | ❌ W0 | ⬜ pending |
| 11-01-01 | 01 | 1 | CHART-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.SeaChartMathTests"` | ❌ W0 | ⬜ pending |
| 11-01-01 | 01 | 1 | EDITOR-01..03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.EditorConfigTests"` | ❌ W0 | ⬜ pending |
| 11-01-02 | 01 | 1 | infra | build | `dotnet build` | n/a | ⬜ pending |
| 11-02-01 | 02 | 2 | PLAYER-01..04 | build | `dotnet build` (VM wiring + BanDialog) | n/a | ⬜ pending |
| 11-02-02 | 02 | 2 | PLAYER-01..04 | build+test | `dotnet test` (regressions) | n/a | ⬜ pending |
| 11-03-01 | 03 | 3 | EVENT-01..03 | build | `dotnet build` (VM) | n/a | ⬜ pending |
| 11-03-02 | 03 | 3 | EVENT-01..03 | build+test | `dotnet test` | n/a | ⬜ pending |
| 11-04-01 | 04 | 4 | CHART-01..02 | build | `dotnet build` (VM) | n/a | ⬜ pending |
| 11-04-02 | 04 | 4 | CHART-01..02 | build+test | `dotnet test` | n/a | ⬜ pending |
| 11-05-01 | 05 | 5 | EDITOR-01..03 | build | `dotnet build` (VM) | n/a | ⬜ pending |
| 11-05-02 | 05 | 5 | EDITOR-01..03 | build+test | `dotnet test` | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All Wave-0 test files are created in Plan 11-01 Task 1. Filenames MUST match exactly:

- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/WindrosePlusApiServiceTests.cs` — tests for RCON body construction, status parsing, kick/ban (permanent + timed)/broadcast command builders, atomic config write
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/EventsLogParserTests.cs` — tests for EVENT-01 through EVENT-03 (TryParseLine + MatchesFilter)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/SeaChartMathTests.cs` — tests for CHART-01 (WorldToCanvas transform)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/EditorConfigTests.cs` — tests for EDITOR-01 through EDITOR-03 (schema + Validate)

*All stubs start RED (failing) and are driven GREEN during Plan 11-01 Task 1 execution — helpers are fully implemented in the same task, so most tests go GREEN immediately.*

Filter verification commands (quick-run only the files listed above):

```bash
dotnet test --filter "FullyQualifiedName~Phase11.WindrosePlusApiServiceTests"
dotnet test --filter "FullyQualifiedName~Phase11.EventsLogParserTests"
dotnet test --filter "FullyQualifiedName~Phase11.SeaChartMathTests"
dotnet test --filter "FullyQualifiedName~Phase11.EditorConfigTests"
# All Phase 11 tests at once:
dotnet test --filter "FullyQualifiedName~Phase11"
```

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Map canvas renders player markers at correct positions | CHART-02 | Avalonia Canvas rendering — no headless test support | Launch app, connect server with WindrosePlus, open Sea-Chart view, verify markers appear at positions matching `/query` response |
| Player kick confirmation dialog appears | PLAYER-02 | UI dialog interaction | Select player → "Kick" → verify ConfirmDialog appears with player name before action executes |
| Player ban dialog with Permanent/Timed toggle | PLAYER-03 | UI dialog interaction with state | Select player → "Ban" → verify BanDialog shows Permanent radio (default) + Timed radio + minutes NumericUpDown; choose Timed + 5 min → verify RCON command sent is `wp.ban {id} 5` |
| Config save prompts restart when server is running | EDITOR-03 | UI state interaction | Start server, edit a value in Config Editor, save → verify restart-required toast appears |
| Events list stays responsive at >1000 entries | EVENT-03 | Performance — requires live data or large log | Load a large events.log (1000+ lines) and verify scroll/filter stays smooth |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references — filenames match Plan 11-01 exactly (WindrosePlusApiServiceTests, EventsLogParserTests, SeaChartMathTests, EditorConfigTests)
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
