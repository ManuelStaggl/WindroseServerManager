---
phase: 11
slug: feature-views
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-20
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
| 11-01-01 | 01 | 1 | PLAYER-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.PlayerApi"` | ❌ W0 | ⬜ pending |
| 11-01-02 | 01 | 1 | PLAYER-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.PlayerApi"` | ❌ W0 | ⬜ pending |
| 11-01-03 | 01 | 1 | PLAYER-02,PLAYER-03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.RconCommands"` | ❌ W0 | ⬜ pending |
| 11-01-04 | 01 | 1 | PLAYER-04 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.PlayerApi"` | ❌ W0 | ⬜ pending |
| 11-02-01 | 02 | 1 | EVENT-01,EVENT-02 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.EventLog"` | ❌ W0 | ⬜ pending |
| 11-02-02 | 02 | 1 | EVENT-03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.EventLog"` | ❌ W0 | ⬜ pending |
| 11-03-01 | 03 | 1 | CHART-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.SeaChart"` | ❌ W0 | ⬜ pending |
| 11-03-02 | 03 | 1 | CHART-02 | manual | n/a — UI rendering | n/a | ⬜ pending |
| 11-04-01 | 04 | 1 | EDITOR-01,EDITOR-02 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.ConfigEditor"` | ❌ W0 | ⬜ pending |
| 11-04-02 | 04 | 1 | EDITOR-03 | unit | `dotnet test --filter "FullyQualifiedName~Phase11.ConfigEditor"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/PlayerApiTests.cs` — stubs for PLAYER-01 through PLAYER-04
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/RconCommandTests.cs` — stubs for PLAYER-02, PLAYER-03 (kick, ban, broadcast)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/EventLogParserTests.cs` — stubs for EVENT-01 through EVENT-03
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/SeaChartTests.cs` — stubs for CHART-01
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/ConfigEditorTests.cs` — stubs for EDITOR-01 through EDITOR-03

*All stubs start RED (failing) and are driven GREEN during plan execution.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Map canvas renders player markers at correct positions | CHART-02 | Avalonia Canvas rendering — no headless test support | Launch app, connect server with WindrosePlus, open Sea-Chart view, verify markers appear at positions matching `/query` response |
| Player kick/ban confirmation dialog appears | PLAYER-03 | UI dialog interaction | Right-click player → "Kick" → verify modal appears with player name before action executes |
| Config save prompts restart when server is running | EDITOR-03 | UI state interaction | Start server, edit a value in Config Editor, save → verify restart prompt dialog appears |
| Events list stays responsive at >1000 entries | EVENT-03 | Performance — requires live data or large log | Load a large events.log (1000+ lines) and verify scroll/filter stays smooth |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
