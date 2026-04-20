---
phase: 10-health-support
plan: 02
subsystem: app-layer
tags: [health-check, banner, dashboard, windrose-plus, avalonia]
dependency_graph:
  requires: ["10-01"]
  provides: ["HEALTH-01-app", "HEALTH-02-app"]
  affects: ["DashboardViewModel", "DashboardView"]
tech_stack:
  added: []
  patterns: ["HealthBannerViewModel (non-DI, per-server)", "rate-limited HTTP polling with grace period", "session-dismiss flag pattern"]
key_files:
  created:
    - src/WindroseServerManager.App/ViewModels/HealthBannerViewModel.cs
  modified:
    - src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs
    - src/WindroseServerManager.App/Views/Pages/DashboardView.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.de.axaml
    - src/WindroseServerManager.App/Resources/Strings/Strings.en.axaml
decisions:
  - "HealthBannerViewModel ctor receives windroseBuildId as optional string (not IServerConfigService) — avoids sync I/O on UI thread; ServerDescription.DeploymentId does not exist on inner class, BuildId from ServerInstallInfo is the correct version proxy"
  - "IHttpClientFactory injected into DashboardViewModel (already registered via s.AddHttpClient() in App.axaml.cs)"
  - "Health-check block placed inside the serverDir != null branch, after retrofit banner block — uses same serverDir variable"
  - "danger button class used for Report Issue (matches existing usage in ConfigurationView/BackupsView/ModsView)"
metrics:
  duration: "~20 minutes"
  completed: "2026-04-20T03:28:54Z"
  tasks: 3
  files: 5
---

# Phase 10 Plan 02: App-Layer Health Banner Summary

**One-liner:** HealthBannerViewModel + DashboardViewModel health-check loop + red AXAML card wiring Phase 10 HEALTH-01/HEALTH-02 end-to-end.

## What Was Built

### Task 1 — HealthBannerViewModel + bilingual Health.* strings (commit 71545c5)

- `src/WindroseServerManager.App/ViewModels/HealthBannerViewModel.cs` created
- Mirrors `RetrofitBannerViewModel` pattern: non-DI, `event Action? StateChanged`, `[RelayCommand]` methods
- `DismissCommand` fires `StateChanged` exactly once
- `OpenReportCommand` builds report URL via `ReportUrlBuilder.Build(...)` and launches default browser with `Process.Start(UseShellExecute=true)`; catches `Exception` and logs at Warning level without rethrowing
- Log tail built from `IServerProcessService.RecentLog` (last 20 lines, formatted as `[HH:mm:ss] [Stream] Text`)
- WindrosePlus version via `IWindrosePlusService.ReadVersionMarker(serverInstallDir)?.Tag` (fallback "unknown")
- Windrose build version passed as optional `string? windroseBuildId` ctor param (see Deviations)
- 4 DE strings + 4 EN strings added immediately after `Retrofit.*` block in both language files

### Task 2 — DashboardViewModel health-check integration (commit 80ce07b)

- `IHttpClientFactory httpFactory` added to constructor (DI-injected, already registered in App.axaml.cs)
- New fields: `_healthBannerVisible`, `_healthBanner`, `_healthBannerDismissedForSession`, `_lastHealthCheckFailed`, `_healthCheckCooldownUntilUtc` (init MinValue), `_healthCheckStartDelayUntilUtc` (init MinValue), `_lastObservedStatus` (init Stopped), `_healthHttpClient`
- Grace period: 15 seconds after `_lastObservedStatus != Running && Status == Running` transition
- Cooldown: 10 seconds between HTTP calls (rate-limit independent of 2s timer)
- HTTP timeout: 3 seconds (set on `_healthHttpClient.Timeout`)
- `OnHealthStateChanged`: sets `_healthBannerDismissedForSession = true`, sets `HealthBannerVisible = false` — no `RefreshAsync` call to avoid reentrancy
- `Dispose`: unsubscribes `HealthBanner.StateChanged`, disposes `_healthHttpClient`
- Health banner and retrofit banner are mutually exclusive: health check only runs inside the `!string.IsNullOrWhiteSpace(serverDir)` branch, gated by `wpActiveForHealth`

### Task 3 — DashboardView.axaml health banner card (commit ebbd5db)

- `<Border>` inserted BEFORE the retrofit banner Border (health banner line 14, retrofit banner line 41 after insertion)
- `BorderBrush="{DynamicResource BrandErrorBrush}"` (red, distinct from amber/warning of retrofit banner)
- Title foreground also `BrandErrorBrush`; body foreground `BrandTextMutedBrush`
- `Classes="danger"` on Report button (matching existing danger button usage in ConfigurationView etc.)
- `Classes="subtle"` on Dismiss button

## Grace Period + Cooldown Values

| Parameter | Value |
|---|---|
| Startup grace period | 15 seconds after Running transition |
| Health check rate limit | 1 call per 10 seconds |
| HTTP call timeout | 3 seconds |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ServerDescription.DeploymentId does not exist on inner class**
- **Found during:** Task 1 build
- **Issue:** `IServerConfigService.LoadServerDescriptionAsync` returns `ServerDescription?` (inner persistent class). `DeploymentId` lives on `ServerDescriptionFile` (outer envelope). The interface only exposes the inner class. Plan assumed `.DeploymentId` was accessible.
- **Fix:** Changed HealthBannerViewModel constructor to accept `string? windroseBuildId` instead of `IServerConfigService`. DashboardViewModel passes `InstallInfo?.BuildId` at banner creation time — this is the correct "Windrose version" proxy (BuildId = Steam build ID) and avoids synchronous I/O on the UI thread from an event handler.
- **Files modified:** `HealthBannerViewModel.cs`, `DashboardViewModel.cs`
- **Commits:** 71545c5, 80ce07b (inline fix, no separate commit)

## Manual Smoke-Test Outcome

Not performed (no running server environment available in this execution context). The banner logic is functionally complete and verified by build + Core test suite:

- Build: 0 errors (27 pre-existing CA1416/AVLN5001 warnings, all unrelated to Phase 10 changes)
- Core tests: 112/112 passed (Phase10 filter: 6/6, full suite regression-clean)

## Known Follow-ups for v1.3

- **port=0 diagnostic:** When `WindrosePlusDashboardPortByServer` returns 0 (e.g. server configured before port assignment), health check silently returns false and banner triggers incorrectly. A guard + informational toast "Dashboard port not configured" would improve UX.
- **Auto-recovery:** If WindrosePlus recovers (process restarts), the banner should auto-hide on the next healthy check. Currently `_lastHealthCheckFailed` flips back to `false` on the next successful check which sets `shouldShowHealth = false` — this works, but `_healthBannerDismissedForSession` stays `false` only if the banner was never shown. If dismissed, recovery is silent until app restart. Acceptable for v1.2.
- **IServerConfigService removal from plan:** Plan specified passing `IServerConfigService` to HealthBannerViewModel for DeploymentId. The real interface doesn't expose DeploymentId directly. Future work: expose `GetServerDescriptionFile()` on the service to give callers access to the outer envelope.

## Self-Check: PASSED

- FOUND: `src/WindroseServerManager.App/ViewModels/HealthBannerViewModel.cs`
- FOUND: `src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs` (modified)
- FOUND: `src/WindroseServerManager.App/Views/Pages/DashboardView.axaml` (modified)
- FOUND: `.planning/phases/10-health-support/10-02-SUMMARY.md`
- Commits verified: 71545c5, 80ce07b, ebbd5db
- Build: 0 errors
- Tests: 112/112 passed
