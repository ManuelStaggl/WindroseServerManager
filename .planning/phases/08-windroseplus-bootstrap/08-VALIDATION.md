---
phase: 8
slug: windroseplus-bootstrap
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. See `08-RESEARCH.md` § Validation Architecture for the source-of-truth test design.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 (already present in `tests/WindroseServerManager.Core.Tests`) |
| **Config file** | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| **Quick run command** | `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~WindrosePlus" --nologo` |
| **Full suite command** | `dotnet test --nologo` |
| **Estimated runtime** | ~30s quick / ~90s full |

---

## Sampling Rate

- **After every task commit:** `dotnet build` + quick filter for the touched service
- **After every plan wave:** Full suite
- **Before `/gsd:verify-work`:** Full suite must be green + one manual install against a real server
- **Max feedback latency:** 90s

---

## Per-Task Verification Map

*Filled during planning — every task below gets a row.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | WPLUS-01..04 | unit/integration | `dotnet test ...` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/WindroseServerManager.Core.Tests/Services/WindrosePlusServiceTests.cs` — stubs for WPLUS-01..04
- [ ] `tests/WindroseServerManager.Core.Tests/Fixtures/FakeGithubReleaseServer.cs` — in-process HTTP stub returning pinned release JSON + archive bytes + digest
- [ ] `tests/WindroseServerManager.Core.Tests/Fixtures/TempServerFixture.cs` — creates a minimal fake server dir on the test temp volume for install/launcher tests
- [ ] `tests/WindroseServerManager.Core.Tests/Fixtures/SampleArchives/` — tiny handcrafted WindrosePlus-shaped + UE4SS-shaped zips for extract/atomic-move tests

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| About dialog shows "Third-Party Licenses" section with WindrosePlus MIT text + HumanGenome link | WPLUS-03 | WPF visual assertion beyond unit-test scope | Launch app → Settings → About → verify section visible in DE + EN, link opens correct repo |
| Real end-to-end install against a freshly-downloaded Windrose dedicated server | WPLUS-01, WPLUS-02, WPLUS-04 | Depends on external Steam content + network | Download server via app → install WindrosePlus → verify `StartWindrosePlusServer.bat` exists → start server → confirm WindrosePlus loads in logs |
| Offline cache fallback on install #2 with network disconnected | WPLUS-01 | Requires real network state toggle | Install once (fills cache) → disable NIC → install again → must succeed from cache |
| First-install offline hard-fail UX (toast + retry) | WPLUS-01 | Requires real network state + UI observation | Fresh cache dir → disable NIC → install → must show German error toast + retry button, no partial server creation |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
