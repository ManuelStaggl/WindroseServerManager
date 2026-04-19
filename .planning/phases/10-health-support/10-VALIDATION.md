---
phase: 10
slug: health-support
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| **Quick run command** | `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase10" -x` |
| **Full suite command** | `dotnet test tests/WindroseServerManager.Core.Tests -x` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase10" -x`
- **After every plan wave:** Run `dotnet test tests/WindroseServerManager.Core.Tests -x`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 1 | HEALTH-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |
| 10-01-02 | 01 | 1 | HEALTH-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |
| 10-01-03 | 01 | 1 | HEALTH-01 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |
| 10-01-04 | 01 | 1 | HEALTH-02 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |
| 10-01-05 | 01 | 1 | HEALTH-02 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |
| 10-01-06 | 01 | 1 | HEALTH-02 | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/WindroseServerManager.Core.Tests/Phase10/HealthCheckTests.cs` — stubs for HEALTH-01 (http timeout / success / port-0 cases)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase10/ReportUrlBuilderTests.cs` — stubs for HEALTH-02 (URL encoding, empty log, truncation)
- [ ] Static `HealthCheckHelper` or `ReportUrlBuilder` class in Core layer with `InternalsVisibleTo` access for tests

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Health banner appears in DashboardView when WindrosePlus HTTP check fails | HEALTH-01 | Requires running server + live HTTP endpoint | Start server with WP active, block port → verify banner appears after 15s grace |
| "Report to WindrosePlus" opens prefilled GitHub issue URL in browser | HEALTH-02 | Requires browser launch + visual URL inspection | Click button → verify browser opens correct GitHub URL with WV/WP versions in body |
| Banner does NOT appear during normal startup (grace period) | HEALTH-01 | Timing-dependent, requires live execution | Start server normally → verify no banner during first 15s of Running state |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
