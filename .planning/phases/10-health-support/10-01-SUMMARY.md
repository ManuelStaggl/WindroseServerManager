---
phase: 10-health-support
plan: "01"
subsystem: testing
tags: [xunit, http-health-check, github-issue-url, core-services, tdd]

requires:
  - phase: 09-opt-in-ux
    provides: Phase9 test patterns (Phase9/ filter convention, FreePortProbe, AppSettingsService)

provides:
  - "HealthCheckHelper.IsHealthyAsync(int port, HttpClient, CancellationToken) -> Task<bool>"
  - "ReportUrlBuilder.Build(windroseVersion, windrosePlusVersion, dashboardPort, logTailLines) -> string"
  - "6 Phase10 xUnit unit tests (HEALTH-01, HEALTH-02)"

affects: [10-02-dashboard-viewmodel, phase-11-feature-views]

tech-stack:
  added: []
  patterns:
    - "Static helper classes in Core.Services — no DI, pure functions, fully unit-testable"
    - "TDD RED→GREEN in two atomic commits (test commit, implementation commit)"
    - "Linked CancellationTokenSource pattern for HTTP safety-net timeout"

key-files:
  created:
    - src/WindroseServerManager.Core/Services/HealthCheckHelper.cs
    - src/WindroseServerManager.Core/Services/ReportUrlBuilder.cs
    - tests/WindroseServerManager.Core.Tests/Phase10/HealthCheckTests.cs
    - tests/WindroseServerManager.Core.Tests/Phase10/ReportUrlBuilderTests.cs
  modified: []

key-decisions:
  - "Both helpers are public static classes — no InternalsVisibleTo needed, tests reach them directly"
  - "HealthCheckHelper uses port <= 0 guard (not == 0) to cover negative port edge cases"
  - "ReportUrlBuilder uses l[..MaxLogLineChars] slice syntax for truncation (C# 8+ range operator)"
  - "No additional defensive null checks added beyond what the plan specified"

patterns-established:
  - "Phase10/ test folder mirrors Phase9/ convention — filter via FullyQualifiedName~Phase10"
  - "StubHandler + DelayHandler + ThrowIfCalledHandler as inner private classes in test file"
  - "DecodeBody() helper extracts URI-decoded body param for assertion clarity"

requirements-completed: [HEALTH-01, HEALTH-02]

duration: 2min
completed: 2026-04-20
---

# Phase 10 Plan 01: Health & Support Core Primitives Summary

**HealthCheckHelper (HTTP probe with linked CTS) and ReportUrlBuilder (prefilled GitHub issue URL) shipped as pure static Core helpers, locked by 6 Phase10 xUnit tests in TDD RED→GREEN.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-04-20T03:21:00Z
- **Completed:** 2026-04-20T03:22:33Z
- **Tasks:** 2 (TDD RED + GREEN)
- **Files modified:** 4 (2 test files, 2 implementation files)

## Accomplishments

- `HealthCheckHelper.IsHealthyAsync` implemented with port-zero guard, 3s linked CTS safety-net, and full exception swallowing (returns false on HttpRequestException / TaskCanceledException / OperationCanceledException)
- `ReportUrlBuilder.Build` implemented with Uri.EscapeDataString title+body, markdown body structure, 20-line cap, 200-char truncation, empty-log placeholder
- 6/6 Phase10 xUnit tests GREEN; full 112-test suite passes with zero regressions

## Public Signatures (for Plan 10-02 consumption)

```csharp
// src/WindroseServerManager.Core/Services/HealthCheckHelper.cs
namespace WindroseServerManager.Core.Services;
public static class HealthCheckHelper
{
    public static async Task<bool> IsHealthyAsync(int port, HttpClient httpClient, CancellationToken ct);
}

// src/WindroseServerManager.Core/Services/ReportUrlBuilder.cs
namespace WindroseServerManager.Core.Services;
public static class ReportUrlBuilder
{
    public const string BaseUrl = "https://github.com/HumanGenome/WindrosePlus/issues/new";
    public const int MaxLogLines = 20;
    public const int MaxLogLineChars = 200;
    public const string EmptyLogPlaceholder = "(no server log available)";

    public static string Build(
        string windroseVersion,
        string windrosePlusVersion,
        int dashboardPort,
        IReadOnlyList<string>? logTailLines);
}
```

## Task Commits

1. **Task 1: Failing Phase10 tests (RED)** - `0eb6fcf` (test)
2. **Task 2: HealthCheckHelper + ReportUrlBuilder implementation (GREEN)** - `b07bb4c` (feat)

**Plan metadata:** _(docs commit follows)_

## Files Created/Modified

- `src/WindroseServerManager.Core/Services/HealthCheckHelper.cs` - HTTP health probe static helper
- `src/WindroseServerManager.Core/Services/ReportUrlBuilder.cs` - GitHub issue URL builder static helper
- `tests/WindroseServerManager.Core.Tests/Phase10/HealthCheckTests.cs` - 3 HEALTH-01 unit tests
- `tests/WindroseServerManager.Core.Tests/Phase10/ReportUrlBuilderTests.cs` - 3 HEALTH-02 unit tests

## Decisions Made

- `port <= 0` guard (not `== 0`) to cover negative port values — strictly more correct
- No `InternalsVisibleTo` needed since both helpers are `public`
- `-x` flag not supported on Windows dotnet CLI — used `--nologo` for verification instead (plan's verify command was Linux-only; behavior identical)

## Deviations from Plan

None — plan executed exactly as written. The `-x` flag incompatibility on Windows dotnet CLI is a verification-command issue only, not a code deviation; GREEN state was confirmed via `--nologo` and exact output inspection.

## Issues Encountered

- `dotnet test ... -x` flag causes MSBuild error on Windows (unknown switch) — verified GREEN state via `dotnet test ... --nologo` instead. Same test results, no impact on implementation.

## Next Phase Readiness

- Plan 10-02 can consume `HealthCheckHelper.IsHealthyAsync` and `ReportUrlBuilder.Build` directly — both are public static, no DI registration needed
- Test suite at 112 tests total; Phase10 slice runs in ~640ms
- No App-layer files modified in this plan

---
*Phase: 10-health-support*
*Completed: 2026-04-20*
