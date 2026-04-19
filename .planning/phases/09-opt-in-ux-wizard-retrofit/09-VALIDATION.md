---
phase: 9
slug: opt-in-ux-wizard-retrofit
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing test project) |
| **Config file** | tests/WindroseServerManager.Tests/WindroseServerManager.Tests.csproj |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Phase9" --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Phase9" --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 9-00-01 | 00 | 0 | WIZARD-03, WIZARD-04, RETRO-01 | scaffold | — | ❌ W0 | ⬜ pending |
| 9-01-xx | 01 | 1 | WIZARD-03 | unit | `dotnet test --filter "FullyQualifiedName~RconPasswordGenerator"` | ❌ W0 | ⬜ pending |
| 9-01-xx | 01 | 1 | WIZARD-03 | unit | `dotnet test --filter "FullyQualifiedName~SteamIdParser"` | ❌ W0 | ⬜ pending |
| 9-01-xx | 01 | 1 | WIZARD-04 | unit | `dotnet test --filter "FullyQualifiedName~FreePortProbe"` | ❌ W0 | ⬜ pending |
| 9-02-xx | 02 | 2 | WIZARD-01, WIZARD-02 | manual smoke | — | — | ⬜ pending |
| 9-03-xx | 03 | 2 | RETRO-01 | unit | `dotnet test --filter "FullyQualifiedName~OptInMigration"` | ❌ W0 | ⬜ pending |
| 9-03-xx | 03 | 3 | RETRO-02, RETRO-03 | manual smoke | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/WindroseServerManager.Tests/Phase9/RconPasswordGeneratorTests.cs` — stubs for WIZARD-03 (password length, charset)
- [ ] `tests/WindroseServerManager.Tests/Phase9/SteamIdParserTests.cs` — stubs for WIZARD-03 (SteamID64, profile URL, vanity reject)
- [ ] `tests/WindroseServerManager.Tests/Phase9/FreePortProbeTests.cs` — stubs for WIZARD-04 (18080–18099 range)
- [ ] `tests/WindroseServerManager.Tests/Phase9/OptInMigrationTests.cs` — stubs for RETRO-01 (seed, idempotent)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Wizard Schritt 2 zeigt Feature-Grid + Opt-out Checkbox | WIZARD-01 | UI/visual | Install-Wizard öffnen, zu Schritt 2 navigieren |
| Retrofit-Banner erscheint bei Server ohne WindrosePlus | RETRO-02 | UI/visual | v1.2 mit v1.1-State starten, Dashboard öffnen |
| "Nicht jetzt" setzt OptedOut, keine Installation | RETRO-03 | UI + side-effect | Banner klicken, "Nicht jetzt" wählen, Dialog-State prüfen |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
