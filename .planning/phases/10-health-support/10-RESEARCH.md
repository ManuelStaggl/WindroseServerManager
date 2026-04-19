# Phase 10: Health & Support — Research

**Researched:** 2026-04-19
**Domain:** HTTP health-check polling, inline banner UI, GitHub issue URL prefill
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| HEALTH-01 | App polls WindrosePlus HTTP dashboard after server start; shows inline banner if endpoint does not respond within health-check window | HttpClient GET to `http://localhost:{port}/api/status`, per-server port from `AppSettings.WindrosePlusDashboardPortByServer`, timeout-based failure detection, banner pattern identical to RetrofitBannerViewModel |
| HEALTH-02 | Banner's "Report to WindrosePlus" button opens a prefilled GitHub issue (Windrose version, WindrosePlus version, server log tail) | GitHub `issues/new?title=...&body=...` URL query-string encoding; Windrose version from `DeploymentId` in `ServerDescription.json`; WindrosePlus version from `WindrosePlusVersionMarker.Tag` via `IWindrosePlusService.ReadVersionMarker()`; server log tail from `IServerProcessService.RecentLog` |
</phase_requirements>

---

## Summary

Phase 10 adds a health-check loop that runs after server start: the app polls `http://localhost:{port}/api/status` (port from `AppSettings.WindrosePlusDashboardPortByServer[serverDir]`) and shows an inline error banner in DashboardView if WindrosePlus does not respond within the health-check window. The banner mirrors the existing RetrofitBanner card pattern — a coloured border, explanatory text, and action buttons — but uses `BrandErrorBrush` instead of `BrandWarningBrush` to signal active incompatibility rather than a pending upgrade offer.

The second requirement adds a "Report to WindrosePlus" button that opens a browser URL pointing to `https://github.com/HumanGenome/WindrosePlus/issues/new` with `title` and `body` query parameters pre-populated. The body contains the Windrose game version (from `ServerDescription.json → DeploymentId`), the WindrosePlus version tag (from `.wplus-version` marker via `IWindrosePlusService.ReadVersionMarker()`), and a tail of the recent server log lines (from `IServerProcessService.RecentLog`).

The entire feature fits in roughly two files: a new `HealthBannerViewModel` in `WindroseServerManager.App/ViewModels/` and minor additions to `DashboardViewModel` + `DashboardView.axaml`. No new service interface is required — everything already exists. The planner should structure this as a single wave (no Wave 0 contracts needed beyond what Phase 8 already shipped).

**Primary recommendation:** One plan, one wave. `HealthBannerViewModel` as a non-DI, per-server object created by `DashboardViewModel`, mirroring the `RetrofitBannerViewModel` construction pattern exactly.

---

## Standard Stack

### Core — already in project, no new dependencies needed

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Net.Http.HttpClient` (via `IHttpClientFactory`) | .NET 9 BCL | HTTP GET to WindrosePlus `/api/status` | Already registered in DI (`s.AddHttpClient()` in App.axaml.cs line 70); `WindrosePlusService` uses the "github" named client; health-check should use an unnamed (default) or a new named client |
| `CommunityToolkit.Mvvm` | already in project | `[ObservableProperty]`, `[RelayCommand]` on `HealthBannerViewModel` | Identical pattern to `RetrofitBannerViewModel` |
| `System.Web.HttpUtility` / `Uri.EscapeDataString` | .NET 9 BCL | URL-encode issue title+body for GitHub URL | `Uri.EscapeDataString` is preferred over `HttpUtility.UrlEncode` because it encodes spaces as `%20` not `+`; GitHub's `issues/new` URL parser handles both, but `%20` is safer in raw URLs opened via `Process.Start(UseShellExecute=true)` |

### No new NuGet packages required

All needed APIs are already available. Confirmed via code search.

**Installation:** none

---

## Architecture Patterns

### Recommended Component Structure

```
ViewModels/
└── HealthBannerViewModel.cs     # New — non-DI, per-server, mirrors RetrofitBannerViewModel
Views/Pages/
└── DashboardView.axaml          # Add health banner Border card (after retrofit banner, before crash warning)
Resources/Strings/
├── Strings.de.axaml             # New Health.* keys
└── Strings.en.axaml             # New Health.* keys
```

`DashboardViewModel.cs` gets two new fields (`_healthBannerVisible`, `_healthBanner`) and health-check logic in `RefreshAsync`.

No new service interface, no new Core project file, no new DI registrations.

### Pattern 1: HealthBannerViewModel — mirrors RetrofitBannerViewModel

**What:** A non-DI ViewModel created by `DashboardViewModel` when WindrosePlus is active and the HTTP dashboard fails to respond.

**When to use:** Instantiated by `DashboardViewModel.RefreshAsync` whenever `WindrosePlusActive == true` and the health-check HTTP GET fails; torn down when the server is stopped or health-check succeeds.

**Key design decisions:**
- NOT registered in DI — takes `string serverInstallDir`, `int dashboardPort`, `IWindrosePlusService`, `IServerProcessService`, `IServerConfigService` as constructor arguments. `DashboardViewModel` uses its own injected references.
- Exposes a `StateChanged` event (same contract as `RetrofitBannerViewModel`) so `DashboardViewModel` can hide the banner immediately on dismiss.
- The `ReportCommand` builds the GitHub URL and calls `Process.Start(UseShellExecute=true)` — no async needed.
- `DismissCommand` sets a transient in-memory flag (`_healthBannerDismissedForSession`) on `DashboardViewModel` so the banner does not reappear on every timer tick during the same session. It reappears after app restart. This is intentional: health problems should stay visible across restarts.

```csharp
// Confidence: HIGH — mirrors existing RetrofitBannerViewModel pattern
public partial class HealthBannerViewModel : ViewModelBase
{
    private readonly IWindrosePlusService _wplus;
    private readonly IServerProcessService _proc;
    private readonly IServerConfigService _config;

    public string ServerInstallDir { get; }
    public int DashboardPort { get; }

    public event Action? StateChanged;

    [RelayCommand]
    private void Dismiss() => StateChanged?.Invoke();

    [RelayCommand]
    private void OpenReport()
    {
        var url = BuildReportUrl();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            // Log only — browser open failure is non-critical
            Serilog.Log.Warning(ex, "Failed to open report URL");
        }
    }

    private string BuildReportUrl()
    {
        var wpVersion  = _wplus.ReadVersionMarker(ServerInstallDir)?.Tag ?? "unknown";
        var windVer    = TryGetWindroseVersion() ?? "unknown";
        var logTail    = BuildLogTail();

        var title = $"[Compat] WindrosePlus not responding after server start (Windrose {windVer}, WP {wpVersion})";
        var body   = $"""
            ## Environment
            - Windrose Version: `{windVer}`
            - WindrosePlus Version: `{wpVersion}`
            - Dashboard Port: `{DashboardPort}`

            ## Server Log Tail (last 20 lines)
            ```
            {logTail}
            ```

            ## Steps to Reproduce
            <!-- Describe what happened -->

            ## Expected Behavior
            WindrosePlus HTTP dashboard responds after server start.
            """;

        return "https://github.com/HumanGenome/WindrosePlus/issues/new"
            + "?title=" + Uri.EscapeDataString(title)
            + "&body="  + Uri.EscapeDataString(body);
    }
}
```

### Pattern 2: Health-check integration in DashboardViewModel.RefreshAsync

**What:** After the existing retrofit banner logic block in `RefreshAsync`, add a parallel health-check block.

**Decision — when to check:**
- Only when: `wpActive == true` AND `Status == ServerStatus.Running`
- Use a `_healthCheckCooldownUntilUtc` field to rate-limit retries (e.g. check at most every 10s even though `_timer` fires every 2s)
- Store last poll result in `_lastHealthCheckFailed` bool; `RefreshAsync` updates it without hammering

**Decision — HTTP client:**
- Use `_httpFactory.CreateClient()` (default, unnamed client) with a short timeout (3 seconds). Do NOT reuse the "github" named client that `WindrosePlusService` holds.
- Timeout means "WindrosePlus not running / incompatible" — log at Warning level, show banner.

```csharp
// In DashboardViewModel.RefreshAsync, after retrofit banner block:
// Confidence: HIGH — based on existing HttpClient patterns in WindrosePlusService

if (wpActive && Status == ServerStatus.Running && DateTime.UtcNow >= _healthCheckCooldownUntilUtc)
{
    _healthCheckCooldownUntilUtc = DateTime.UtcNow.AddSeconds(10);
    var healthy = await CheckWindrosePlusHealthAsync(serverDir, ct);
    _lastHealthCheckFailed = !healthy;
}

// Show/hide health banner (independent of cooldown):
if (wpActive && Status == ServerStatus.Running && _lastHealthCheckFailed && !_healthBannerDismissedForSession)
{
    if (HealthBanner is null || HealthBanner.ServerInstallDir != serverDir)
    {
        if (HealthBanner is not null)
            HealthBanner.StateChanged -= OnHealthStateChanged;
        HealthBanner = new HealthBannerViewModel(serverDir, port, _wplus, _proc, _config);
        HealthBanner.StateChanged += OnHealthStateChanged;
    }
    HealthBannerVisible = true;
}
else
{
    HealthBannerVisible = false;
}
```

```csharp
private async Task<bool> CheckWindrosePlusHealthAsync(string serverDir, CancellationToken ct)
{
    var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);
    if (port == 0) return false; // port unknown = treat as unhealthy, banner fires

    try
    {
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(3);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        var resp = await http.GetAsync($"http://localhost:{port}/api/status", cts.Token);
        return resp.IsSuccessStatusCode;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
    {
        Log.Warning("WindrosePlus health check failed on port {Port}: {Message}", port, ex.Message);
        return false;
    }
}
```

### Pattern 3: DashboardView AXAML — health banner card

Placed between the retrofit banner card and the crash warning card.

```xml
<!-- Health Banner (WindrosePlus not responding after server start) -->
<Border Classes="card" Margin="0,0,0,16"
        IsVisible="{Binding HealthBannerVisible}"
        BorderBrush="{DynamicResource BrandErrorBrush}" BorderThickness="1">
    <Grid ColumnDefinitions="*,Auto">
        <StackPanel Grid.Column="0" Spacing="6">
            <TextBlock Classes="section-header"
                       Foreground="{DynamicResource BrandErrorBrush}"
                       Text="{DynamicResource Health.Banner.Title}" />
            <TextBlock Classes="body"
                       Foreground="{DynamicResource BrandTextMutedBrush}"
                       TextWrapping="Wrap"
                       Text="{DynamicResource Health.Banner.Body}" />
        </StackPanel>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8"
                    VerticalAlignment="Center">
            <Button Classes="danger"
                    Content="{DynamicResource Health.Banner.Action.Report}"
                    Command="{Binding HealthBanner.OpenReportCommand}" />
            <Button Classes="subtle"
                    Content="{DynamicResource Health.Banner.Action.Dismiss}"
                    Command="{Binding HealthBanner.DismissCommand}" />
        </StackPanel>
    </Grid>
</Border>
```

**Confidence: HIGH** — direct clone of retrofit banner pattern already in DashboardView.axaml lines 13–34.

### Pattern 4: Windrose version resolution

**Source:** `ServerDescription.json → DeploymentId` field (established in `project_windrose_server_facts.md`).

```csharp
// Confidence: HIGH — existing IServerConfigService already loads ServerDescription
private async Task<string?> TryGetWindroseVersionAsync()
{
    try
    {
        var desc = await _config.LoadServerDescriptionAsync(CancellationToken.None);
        return desc?.DeploymentId; // e.g. "0.10.0-CL12345"
    }
    catch { return null; }
}
```

`IServerConfigService` and its `LoadServerDescriptionAsync` method already exist in the codebase (used in `DashboardViewModel.RefreshAsync` lines 265–291). No new service needed.

### Pattern 5: Server log tail

**Source:** `IServerProcessService.RecentLog` — already a `IReadOnlyList<ServerLogLine>` snapshot. Take the last 20 lines.

```csharp
private string BuildLogTail()
{
    var lines = _proc.RecentLog;
    var tail = lines.Count <= 20 ? lines : lines.Skip(lines.Count - 20);
    return string.Join('\n', tail.Select(l => $"[{l.TimestampUtc:HH:mm:ss}] [{l.Stream}] {l.Text}"));
}
```

**Confidence: HIGH** — `ServerLogLine` record and `IServerProcessService.RecentLog` verified in source.

### Anti-Patterns to Avoid

- **Don't poll on every timer tick without a cooldown.** The 2-second timer fires constantly; a 3-second HTTP timeout per tick would stack. Use the `_healthCheckCooldownUntilUtc` field pattern.
- **Don't show the banner when the server is stopped.** Only show when `Status == ServerStatus.Running` — a stopped server naturally has no HTTP dashboard.
- **Don't show both retrofit banner and health banner simultaneously.** Health banner is only relevant when `wpActive == true`; retrofit banner is only relevant when `wpActive == false`. These are mutually exclusive by definition.
- **Don't dismiss the banner permanently.** Health problems reappear on next app start. In-memory session dismiss flag only (`_healthBannerDismissedForSession` bool on `DashboardViewModel`).
- **Don't hardcode the GitHub issue URL.** Keep it as a `private const string ReportUrl = "https://github.com/HumanGenome/WindrosePlus/issues/new"` in `HealthBannerViewModel` so it's easy to update.
- **Don't call `_config.LoadServerDescriptionAsync` inside a tight loop.** Cache the result from the most recent `DashboardViewModel.RefreshAsync` cycle (already stored in `InstallInfo` / local state).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URL-encode issue body | Custom encoder | `Uri.EscapeDataString(string)` | BCL, handles all Unicode edge-cases, no dependency |
| Open URL in browser | Shell command string | `Process.Start(new ProcessStartInfo { UseShellExecute = true })` | Already used for `OpenServerDir`, `OpenCrashLog` in the same file |
| HTTP GET with timeout | Manual socket | `IHttpClientFactory.CreateClient()` + `http.Timeout` | Already registered in DI; `WindrosePlusService` uses the same factory |
| Windrose version | Binary inspection | `IServerConfigService.LoadServerDescriptionAsync` → `DeploymentId` | Already in-use in `DashboardViewModel.RefreshAsync` |
| WindrosePlus version | File path string | `IWindrosePlusService.ReadVersionMarker(dir).Tag` | Already in Phase 8 contract |

**Key insight:** Every primitive needed for Phase 10 already exists. The work is pure assembly — wiring existing services into a new ViewModel and a new AXAML block.

---

## Common Pitfalls

### Pitfall 1: Health-check fires while server is starting
**What goes wrong:** Server start kicks off `StartAsync`, then `RefreshAsync` timer fires. Status may transition `Stopped → Starting → Running` over several ticks. Health check would immediately fire against a not-yet-started WindrosePlus and show a false banner.
**Why it happens:** The timer interval (2s) and process start time (~5–10s for a Windrose UE5 binary) overlap.
**How to avoid:** Gate health-check on `Status == ServerStatus.Running` only. Add a `_healthCheckStartDelayUntilUtc` that is set to `DateTime.UtcNow + TimeSpan.FromSeconds(15)` whenever `Status` transitions from anything to `Running`. Health-check is skipped until that timestamp passes.
**Warning signs:** Banner appears for <10 seconds after server start then disappears.

### Pitfall 2: Port 0 — wizard or retrofit did not capture port
**What goes wrong:** A server installed by Phase 8 before Phase 9 ran the port-assignment wizard may have `WindrosePlusDashboardPortByServer[dir] == 0` (default/missing).
**Why it happens:** Phase 9 port assignment only ran for new wizard-installs and retrofit-confirmed installs. Existing Phase 8 entries lack the port.
**How to avoid:** `CheckWindrosePlusHealthAsync` treats port `0` as "unknown → unhealthy → show banner". Banner body text should mention "port not configured" as a possible cause. A fallback: try reading `windrose_plus.json` from the server dir and parse `[RCON]` section for the port — but this is optional complexity; port-0 case is rare.
**Warning signs:** Health banner fires immediately for all Phase-8-only servers.

### Pitfall 3: GitHub URL exceeds browser/OS limit with long log tail
**What goes wrong:** URL query string with 20 lines of server log easily reaches 3–5 KB. Some browsers/OS shell invocations truncate URLs at 2048 or 8192 characters.
**Why it happens:** `Uri.EscapeDataString` triples the byte size of spaces/newlines/brackets.
**How to avoid:** Cap log tail to 20 lines AND truncate each line to 200 chars. Total body budget ~4000 chars. The GitHub `issues/new` page handles up to ~65 KB in the body param — the OS/browser layer is the real limit.
**Warning signs:** Issue opens with truncated body.

### Pitfall 4: HttpClient timeout accumulation on UI thread
**What goes wrong:** `CheckWindrosePlusHealthAsync` blocks for 3s on each timer tick if WindrosePlus is down. Even `async Task`, the awaited call holds the timer callback thread.
**Why it happens:** `System.Timers.Timer` callbacks run on ThreadPool, not UI thread — but accumulated awaiting tasks can pile up if the 2s interval fires before the 3s timeout resolves.
**How to avoid:** Use `CancellationTokenSource.CreateLinkedTokenSource(ct)` with `CancelAfter(3s)`, AND the `_healthCheckCooldownUntilUtc` field so only one check is in-flight per 10s window. If the previous check is still pending, skip this tick.

### Pitfall 5: `_proc.RecentLog` may be empty at report time
**What goes wrong:** If the server crashed or was never started, `RecentLog` is empty — the report body has no log section.
**Why it happens:** `IServerProcessService.RecentLog` is a live snapshot; after process death it may be cleared.
**How to avoid:** Gracefully handle empty case: include `(no server log available)` placeholder in the `BuildLogTail` result. Always check `.Count > 0`.

---

## Code Examples

### Building the GitHub issue URL

```csharp
// Source: BCL Uri.EscapeDataString + GitHub issues/new URL format
// Verified: GitHub docs https://docs.github.com/en/issues/tracking-your-work-with-issues/creating-an-issue
private const string ReportBaseUrl = "https://github.com/HumanGenome/WindrosePlus/issues/new";

private string BuildReportUrl(string wpVersion, string windVer, string logTail)
{
    var title = $"[Compat] WindrosePlus not responding — Windrose {windVer} / WP {wpVersion}";
    var body = $"""
        ## Environment
        - Windrose Version: `{windVer}`
        - WindrosePlus Version: `{wpVersion}`
        - Dashboard Port: `{DashboardPort}`
        - Reported by: Windrose Server Manager

        ## Server Log Tail (last 20 lines)
        ```
        {logTail}
        ```

        ## Steps to Reproduce
        1. Start server with WindrosePlus active
        2. WindrosePlus HTTP dashboard does not respond

        ## Expected Behavior
        Dashboard responds on http://localhost:{DashboardPort}/api/status
        """;

    return ReportBaseUrl
        + "?title=" + Uri.EscapeDataString(title)
        + "&body="  + Uri.EscapeDataString(body);
}
```

### Opening the URL

```csharp
// Source: existing DashboardViewModel.OpenServerDir pattern (line 480)
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = url,
    UseShellExecute = true,
});
```

### State-change wiring in DashboardViewModel

```csharp
// Source: existing RetrofitBanner pattern (DashboardViewModel lines 313–325)
[ObservableProperty] private bool _healthBannerVisible;
[ObservableProperty] private HealthBannerViewModel? _healthBanner;
private bool _healthBannerDismissedForSession;
private DateTime _healthCheckCooldownUntilUtc = DateTime.MinValue;
private DateTime _healthCheckStartDelayUntilUtc = DateTime.MinValue;
private bool _lastHealthCheckFailed;

private void OnHealthStateChanged()
{
    _healthBannerDismissedForSession = true;
    _ = RefreshAsync(CancellationToken.None);
}

public void Dispose()
{
    // Existing dispose...
    if (HealthBanner is not null)
        HealthBanner.StateChanged -= OnHealthStateChanged;
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Poll server with fixed 5s timeout | Short timeout (3s) + cooldown gate | Phase 10 | Prevents timer pileup |
| Always show error banner on first miss | Delay banner until after startup grace period | Phase 10 | Eliminates false positives during boot |
| Open static GitHub issue link | Prefilled URL with version + log tail | Phase 10 (HEALTH-02) | Reduces friction for upstream bug reports |

---

## Open Questions

1. **What is the correct `windrose_plus.json` RCON section structure for port fallback?**
   - What we know: maintainer memory states `windrose_plus.json[RCON]` holds the RCON password. The HTTP dashboard port may also be there, or it may be the same port as RCON.
   - What's unclear: Whether the dashboard HTTP port is explicitly written to `windrose_plus.json` by the Phase 9 wizard (it's stored in `AppSettings.WindrosePlusDashboardPortByServer` — not necessarily written to the JSON file).
   - Recommendation: Use `AppSettings.WindrosePlusDashboardPortByServer[dir]` as the authoritative source (Phase 9 already populates it). Do NOT parse `windrose_plus.json` in Phase 10 — that adds complexity. Document as future improvement.

2. **Should the health banner auto-hide when WindrosePlus recovers mid-session?**
   - What we know: The 2-second timer will eventually pick up a successful health-check.
   - What's unclear: User expectation — should the banner disappear automatically if WP self-recovers?
   - Recommendation: Yes — the existing `_lastHealthCheckFailed` boolean is updated on every tick. When it flips to `false`, `RefreshAsync` sets `HealthBannerVisible = false` automatically. No special logic needed.

3. **Grace period duration — how long does WindrosePlus take to start?**
   - What we know: Windrose itself takes ~5–10s to fully start the UE5 binary. WindrosePlus (UE4SS mod) initializes after UE4 engine init, which adds a few more seconds.
   - What's unclear: The exact delay between `StartAsync()` completing and WindrosePlus HTTP dashboard becoming responsive.
   - Recommendation: Use a 15-second grace period after `Status` transitions to `Running`. This is conservative and avoids false positives. Configurable via a `private const int HealthCheckGraceSeconds = 15`.

---

## Localization Keys Required

### Strings.de.axaml (new keys)
```xml
<sys:String x:Key="Health.Banner.Title">WindrosePlus antwortet nicht</sys:String>
<sys:String x:Key="Health.Banner.Body">Nach dem Serverstart konnte kein Kontakt zum WindrosePlus-Dashboard hergestellt werden. Möglicherweise ist WindrosePlus mit der aktuellen Windrose-Version inkompatibel.</sys:String>
<sys:String x:Key="Health.Banner.Action.Report">Problem melden</sys:String>
<sys:String x:Key="Health.Banner.Action.Dismiss">Verbergen</sys:String>
```

### Strings.en.axaml (new keys)
```xml
<sys:String x:Key="Health.Banner.Title">WindrosePlus not responding</sys:String>
<sys:String x:Key="Health.Banner.Body">The WindrosePlus HTTP dashboard did not respond after server start. WindrosePlus may be incompatible with the current Windrose version.</sys:String>
<sys:String x:Key="Health.Banner.Action.Report">Report Issue</sys:String>
<sys:String x:Key="Health.Banner.Action.Dismiss">Dismiss</sys:String>
```

---

## Validation Architecture

`workflow.nyquist_validation` is absent from `.planning/config.json` → treat as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| Quick run command | `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase10" -x` |
| Full suite command | `dotnet test tests/WindroseServerManager.Core.Tests -x` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HEALTH-01 | `CheckWindrosePlusHealthAsync` returns `false` when HTTP GET times out | unit | `dotnet test --filter "FullyQualifiedName~Phase10" -x` | ❌ Wave 0 |
| HEALTH-01 | `CheckWindrosePlusHealthAsync` returns `true` when server responds 200 | unit | same | ❌ Wave 0 |
| HEALTH-01 | Port 0 → returns `false` without making HTTP call | unit | same | ❌ Wave 0 |
| HEALTH-02 | `BuildReportUrl` encodes title + body correctly for GitHub | unit | same | ❌ Wave 0 |
| HEALTH-02 | `BuildReportUrl` with empty log tail includes placeholder | unit | same | ❌ Wave 0 |
| HEALTH-02 | `BuildReportUrl` truncates long log lines to avoid URL overflow | unit | same | ❌ Wave 0 |

> Note: `HealthBannerViewModel.OpenReportCommand` and `DismissCommand` are App-layer — tested manually; the URL-building logic lives in a `internal static` helper method that Core tests can reach via `InternalsVisibleTo`.

### Sampling Rate

- **Per commit:** `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase10" -x`
- **Per wave merge:** `dotnet test tests/WindroseServerManager.Core.Tests -x`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/WindroseServerManager.Core.Tests/Phase10/HealthCheckTests.cs` — covers HEALTH-01 (http timeout / success / port-0 cases)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase10/ReportUrlBuilderTests.cs` — covers HEALTH-02 (URL encoding, empty log, truncation)
- [ ] `HealthCheckHelper` or extraction of `BuildReportUrl` into a static class in Core so tests can access without App reference

---

## Plan Recommendation

Phase 10 fits in **one plan** (one wave):

**Wave 1 — single plan (`10-01-PLAN.md`):**
1. Add `Phase10/` test files (HealthCheckTests, ReportUrlBuilderTests) — pure Core, no App needed
2. Add `HealthBannerViewModel.cs` to App/ViewModels
3. Add `_healthBannerVisible`, `_healthBanner`, health-check fields + `CheckWindrosePlusHealthAsync` + logic block to `DashboardViewModel`
4. Add health banner `<Border>` card to `DashboardView.axaml`
5. Add `Health.*` string keys to both `Strings.de.axaml` + `Strings.en.axaml`
6. Wire `HealthBanner.StateChanged` in `Dispose()`

No Wave 0 contracts needed — all interfaces (`IWindrosePlusService`, `IServerProcessService`, `IServerConfigService`) were established in Phases 8–9.

---

## Sources

### Primary (HIGH confidence)
- Codebase: `src/WindroseServerManager.App/ViewModels/RetrofitBannerViewModel.cs` — direct pattern source
- Codebase: `src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs` — integration pattern
- Codebase: `src/WindroseServerManager.App/Views/Pages/DashboardView.axaml` — AXAML card pattern
- Codebase: `src/WindroseServerManager.Core/Services/IWindrosePlusService.cs` — `ReadVersionMarker` API
- Codebase: `src/WindroseServerManager.Core/Services/IServerProcessService.cs` — `RecentLog` API
- Codebase: `src/WindroseServerManager.Core/Models/AppSettings.cs` — `WindrosePlusDashboardPortByServer`
- Memory: `project_windroseplus_maintainer_agreement.md` — confirms `/api/status` endpoint stability, Report-button prefill fields
- Memory: `project_windrose_server_facts.md` — confirms `DeploymentId` in `ServerDescription.json`

### Secondary (MEDIUM confidence)
- GitHub docs: `https://docs.github.com/en/issues/tracking-your-work-with-issues/creating-an-issue` — confirms `issues/new?title=&body=` URL pattern
- BCL docs: `Uri.EscapeDataString` preferred over `HttpUtility.UrlEncode` for raw URL assembly

### Tertiary (LOW confidence)
- Windrose startup timing (15s grace period) — estimated from UE5 engine init knowledge, not measured against actual Windrose binary

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in project, no new deps
- Architecture: HIGH — direct clone of RetrofitBanner pattern, all APIs verified in source
- Pitfalls: HIGH (pitfalls 1–3), MEDIUM (pitfall 4–5) — timer/HttpClient interaction is well-understood; log-tail edge cases verified via RecentLog API
- Localization keys: HIGH — follows existing `Retrofit.Banner.*` naming convention exactly

**Research date:** 2026-04-19
**Valid until:** 2026-05-19 (stable domain — WindrosePlus API declared stable by maintainer)
