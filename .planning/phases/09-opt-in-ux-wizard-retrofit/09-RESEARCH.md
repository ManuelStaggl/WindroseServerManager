# Phase 9: Opt-in UX (Wizard + Retrofit) — Research

**Researched:** 2026-04-19
**Domain:** Avalonia UI — Multi-step modal wizard, retrofit banner, per-server state persistence
**Confidence:** HIGH (all findings from direct codebase inspection)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Wizard Shape & Placement**
- Form: New modal stepper dialog (FluentWindow / `Window`) — not a redesign of `InstallationView`
- Flow: Install path → WindrosePlus (default-on, feature list, secrets) → Confirm (3 steps)
- Dedicated ViewModel: `InstallWizardViewModel` owns the flow; calls `IServerInstallService` then `IWindrosePlusService.InstallAsync` in sequence
- Existing `InstallationView` + `InstallationViewModel` stays unchanged (re-install / status / update)
- Trigger: `InstallationView` gains a "New server" button that opens the wizard

**RCON Password UX**
- Auto-generated on wizard entry (cryptographically random, URL-safe, ≥24 chars)
- Displayed masked with eye-toggle reveal, copy-to-clipboard, and regenerate buttons
- Stored in `AppSettings.WindrosePlusRconPasswordByServer` (per-server dict)
- Written into `windrose_plus.json[RCON]` at install time

**Dashboard Port UX**
- Auto-picked from free port in range 18080–18099 on wizard entry (first free wins)
- Shown read-only with "change in Settings" caption
- Never hardcoded (WIZARD-04)

**Admin Steam-ID UX**
- Accepts raw SteamID64 (17 digits) OR full Steam profile URL (numeric `/profiles/<id>`)
- Regex extraction only — no Steam Web API call
- Inline validation on blur; vanity URLs without numeric ID rejected
- Required field (WIZARD-03)

**Storage (new per-server fields)**
- `WindrosePlusRconPasswordByServer : Dictionary<string,string>`
- `WindrosePlusDashboardPortByServer : Dictionary<string,int>`
- `WindrosePlusAdminSteamIdByServer : Dictionary<string,string>`
- `WindrosePlusOptInStateByServer : Dictionary<string, OptInState>` — enum: `NeverAsked`, `OptedIn`, `OptedOut`
- `ServerInstallInfo` record extended with read-through properties for these four fields
- Install writes password + port + admin SteamID into `windrose_plus.json` in server directory

**Retrofit Flow**
- Surface: Non-modal `InfoBar`-style banner at top of `DashboardView` (not modal, no app-launch queue)
- Appears when `WindrosePlusOptInState == NeverAsked` AND `WindrosePlusActive == false`
- Actions: "Install WindrosePlus" → opens `RetrofitDialog`; "Not now" → sets `OptedOut`
- No close-X — every interaction is an explicit decision (RETRO-03)
- Post-decision: `OptedIn` → install runs, banner disappears; `OptedOut` → hidden permanently
- Migration rule (first v1.2 launch): all existing servers initialized to `OptInState.NeverAsked`

**Feature List & Copy**
- Layout: 6-tile 2-column icon grid (Players, Kick & Ban, Broadcast, Events, Sea-Chart, INI-Editor)
- Link: "WindrosePlus by HumanGenome · MIT · Learn more ↗" → `https://github.com/HumanGenome/WindrosePlus`
- Tone: Plain, neutral — no pirate/nautical theming
- Disclaimer: one-liner near opt-in toggle about MIT mod + can be disabled
- Bilingual from day one: `Wizard.WindrosePlus.*`, `Retrofit.WindrosePlus.*`, `Feature.*` keys

**Timing**
- WindrosePlus decision + secrets collected before SteamCMD starts
- SteamCMD and WindrosePlus install run unattended to completion

### Claude's Discretion
- Exact wizard step count and titles (2 vs 3 steps acceptable; 3 locked by UI-SPEC)
- Concrete password character set (URL-safe, ≥24 chars)
- Regex(es) for Steam-ID parsing — 5s timeout required by coding standards
- InfoBar styling / icon selection for retrofit banner
- Whether wizard-step UserControl is reused literally in retrofit dialog or duplicated
- Error-state copy for install failures during wizard (follow `ErrorMessageHelper` patterns)

### Deferred Ideas (OUT OF SCOPE)
- Health-check banner (HEALTH-01/02) — Phase 10
- Empty-state CTAs in feature views — Phase 12
- Settings-page per-server WindrosePlus toggle — Phase 12
- Background upstream version check / auto-upgrade — UPGRADE-01, v1.3
- Change WindrosePlus state on existing running server — Phase 11
- Steam Web API vanity-URL resolution — out of scope forever
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| WIZARD-01 | New-server wizard includes a WindrosePlus step listing features gained (Kick/Ban/Broadcast/Events/Chart/INI-Editor) with a link to the WindrosePlus GitHub | `WindrosePlusOptInControl` UserControl — 6-tile grid, GitHub link button using existing `subtle` button pattern + `OpenUrl` helper |
| WIZARD-02 | WindrosePlus is default-on in the wizard; user can opt-out with one click | CheckBox with `IsChecked=True`, controls `IsVisible` of Secrets-Form via binding |
| WIZARD-03 | Wizard sets a secure random RCON password and captures the admin Steam-ID on confirmation | `RandomNumberGenerator.GetBytes` for password; regex validation on Steam-ID field; stored in AppSettings dicts + written to `windrose_plus.json` |
| WIZARD-04 | Wizard picks a free local port for the WindrosePlus dashboard (no fixed port) | `TcpListener`-based free-port probe in range 18080–18099; stored in `AppSettings.WindrosePlusDashboardPortByServer` |
| RETRO-01 | First launch after upgrading to v1.2 detects per server whether WindrosePlus is installed | `IAppSettingsService.MigrateAsync` seeds `OptInState.NeverAsked` for all servers in `WindrosePlusActiveByServer` where key is absent in `WindrosePlusOptInStateByServer` |
| RETRO-02 | For servers without WindrosePlus, a non-modal dialog offers installation with feature list + opt-out; the choice persists per server | Retrofit banner in `DashboardView` (bound to `RetrofitBannerVisible`); `RetrofitDialog` reuses `WindrosePlusOptInControl`; state persisted via `IAppSettingsService.SaveAsync` |
| RETRO-03 | Retrofit never installs silently; user must explicitly confirm | No close-X on banner; RetrofitDialog requires "Install" button click; "Not now" only sets `OptedOut`, no install |
</phase_requirements>

---

## Summary

Phase 9 adds two entry points for WindrosePlus opt-in: (1) a 3-step install wizard for new servers and (2) a retrofit banner + dialog for servers carried over from v1.0/v1.1. Both surfaces share a single `WindrosePlusOptInControl` UserControl containing the feature grid, disclaimer, and secrets form.

The codebase already has all required infrastructure: `IWindrosePlusService.InstallAsync` with `IProgress<InstallProgress>`, per-server AppSettings dictionaries (Phase 8), bilingual string plumbing, `ConfirmDialog`/`AboutDialog` as dialog pattern references, and `ToastService`/`ErrorMessageHelper` for feedback. Phase 9 introduces the first multi-step modal dialog in the app and the first retrofit migration hook.

**Primary recommendation:** Build `InstallWizardViewModel` as a Transient with observable `CurrentStep` int driving step-content visibility; reuse `WindrosePlusOptInControl` in both `InstallWizardWindow` and `RetrofitDialog`; perform AppSettings migration in `IAppSettingsService.LoadAsync` on first v1.2 start.

---

## Standard Stack

### Core (already in project — no new dependencies)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| Avalonia UI | existing | UI framework — `Window`, `UserControl`, bindings | Already registered |
| CommunityToolkit.Mvvm | existing | `[ObservableProperty]`, `[RelayCommand]` | Already used |
| Microsoft.Extensions.DependencyInjection | existing | DI — `AddTransient`, `AddSingleton` | Already used |
| Serilog | existing | Structured logging | Already used |

### No New NuGet Packages Required

All Phase 9 functionality is achievable with existing dependencies:
- Cryptographic password generation: `System.Security.Cryptography.RandomNumberGenerator` (BCL)
- Free-port probe: `System.Net.Sockets.TcpListener` (BCL)
- Regex Steam-ID parsing: `System.Text.RegularExpressions.Regex` with `TimeSpan` timeout (BCL + coding standard)
- URL open: existing `System.Diagnostics.Process.Start` with `UseShellExecute = true` (BCL)

---

## Architecture Patterns

### Recommended Project Structure (new files only)

```
src/WindroseServerManager.App/
├── Views/
│   ├── Dialogs/
│   │   ├── InstallWizardWindow.axaml          # 3-step wizard (new)
│   │   ├── InstallWizardWindow.axaml.cs       # code-behind (new)
│   │   └── RetrofitDialog.axaml               # retrofit install dialog (new)
│   │   └── RetrofitDialog.axaml.cs            # code-behind (new)
│   └── Controls/
│       └── WindrosePlusOptInControl.axaml     # shared UserControl (new)
│       └── WindrosePlusOptInControl.axaml.cs  # code-behind (new)
├── ViewModels/
│   ├── InstallWizardViewModel.cs              # Transient (new)
│   └── RetrofitBannerViewModel.cs             # Transient per server (new)
└── Resources/Strings/
    ├── Strings.de.axaml                       # extend with Wizard.*, Retrofit.*, Feature.* keys
    └── Strings.en.axaml                       # extend with same keys

src/WindroseServerManager.Core/
└── Models/
    ├── AppSettings.cs                         # extend: 4 new dicts + OptInState enum
    └── ServerInstallInfo.cs                   # extend: 4 new read-through properties
```

### Pattern 1: Multi-Step Wizard ViewModel

The wizard state is a simple `CurrentStep` int (1, 2, 3). Each step's content panel uses `IsVisible="{Binding CurrentStep, Converter=...}"` or code-behind `IsVisible` toggle driven by the ViewModel. No complex wizard framework needed.

```csharp
// InstallWizardViewModel.cs
public partial class InstallWizardViewModel : ViewModelBase
{
    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private string _installDir = string.Empty;
    [ObservableProperty] private bool _isOptingIn = true;
    [ObservableProperty] private string _rconPassword = string.Empty;
    [ObservableProperty] private int _dashboardPort;
    [ObservableProperty] private string _adminSteamId = string.Empty;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _currentPhase = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public bool CanGoNext => CurrentStep switch
    {
        1 => !string.IsNullOrWhiteSpace(InstallDir),
        2 => !IsOptingIn || IsSteamIdValid(),
        _ => false
    };

    [RelayCommand]
    private void GoNext() { if (CanGoNext) CurrentStep++; }

    [RelayCommand]
    private void GoBack() { if (CurrentStep > 1) CurrentStep--; }

    [RelayCommand]
    private async Task InstallAsync(CancellationToken ct) { ... }
}
```

### Pattern 2: Shared UserControl with ViewModel binding

`WindrosePlusOptInControl` binds to `InstallWizardViewModel` (wizard) and `RetrofitBannerViewModel` (retrofit dialog). Both expose the same properties: `IsOptingIn`, `RconPassword`, `DashboardPort`, `AdminSteamId`, `SteamIdError`.

The control uses `DataContext` inheritance — no explicit VM type in the control itself. Both wizard and retrofit dialog set the correct VM as their `DataContext`.

### Pattern 3: Dialog Opening Pattern (matches existing codebase)

```csharp
// In InstallationViewModel (adding "New server" trigger)
[RelayCommand]
private async Task OpenNewServerWizardAsync()
{
    var owner = GetOwnerWindow();
    if (owner is null) return;
    var vm = new InstallWizardViewModel(_installSvc, _wplusSvc, _settings, _toasts);
    var dialog = new InstallWizardWindow { DataContext = vm };
    await dialog.ShowDialog(owner);
}
```

This matches `ConfirmDialog.ShowAsync` and `AboutDialog` patterns already in codebase.

### Pattern 4: AppSettings Migration on Load

```csharp
// In AppSettingsService.LoadAsync (or a dedicated MigrateAsync called after load)
private void MigrateToV12(AppSettings settings)
{
    // Seed OptInState.NeverAsked for every server not yet in the new dict
    foreach (var serverDir in settings.WindrosePlusActiveByServer.Keys)
    {
        if (!settings.WindrosePlusOptInStateByServer.ContainsKey(serverDir))
            settings.WindrosePlusOptInStateByServer[serverDir] = OptInState.NeverAsked;
    }
}
```

### Pattern 5: Free-Port Probe

```csharp
private static int FindFreePort(int rangeStart = 18080, int rangeEnd = 18099)
{
    for (int port = rangeStart; port <= rangeEnd; port++)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return port;
        }
        catch (System.Net.Sockets.SocketException) { /* port in use */ }
    }
    // Fallback: OS-assigned port
    var fallback = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    fallback.Start();
    int assigned = ((System.Net.IPEndPoint)fallback.LocalEndpoint).Port;
    fallback.Stop();
    return assigned;
}
```

### Pattern 6: RCON Password Generation

```csharp
private static string GenerateRconPassword(int length = 24)
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);
    return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
}
```

URL-safe character set (no `+`, `/`, `=`); 24 chars from 64-char alphabet = ~144 bits of entropy.

### Pattern 7: Steam-ID Regex (with 5s timeout per coding standards)

```csharp
private static readonly Regex SteamIdRegex = new(
    @"^(7656119\d{10}|https?://steamcommunity\.com/profiles/(7656119\d{10})/?(\?.*)?$)",
    RegexOptions.Compiled,
    TimeSpan.FromSeconds(5));

public static string? ExtractSteamId64(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return null;
    var m = SteamIdRegex.Match(input.Trim());
    if (!m.Success) return null;
    // Group 1 = raw SteamID64, Group 2 = from URL
    return m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value;
}
```

Note: SteamID64 always starts with `7656119` (profile universe + account type). 17 total digits.

### Pattern 8: DashboardView Retrofit Banner Integration

`DashboardView` gains a banner zone at the top of the `StackPanel`, directly after the page-title block and before the crash warning. `DashboardViewModel` gets two new observable properties:

```csharp
[ObservableProperty] private bool _retrofitBannerVisible;
[ObservableProperty] private RetrofitBannerViewModel? _retrofitBannerVm;
```

Banner visibility is computed from AppSettings on each `RefreshAsync` call:

```csharp
// In DashboardViewModel.RefreshAsync
var serverDir = cfg.ServerInstallDir;
if (!string.IsNullOrWhiteSpace(serverDir))
{
    var state = _settings.Current.WindrosePlusOptInStateByServer
        .GetValueOrDefault(serverDir, OptInState.NeverAsked);
    var active = _settings.Current.WindrosePlusActiveByServer
        .GetValueOrDefault(serverDir, false);
    RetrofitBannerVisible = state == OptInState.NeverAsked && !active;
}
```

### Anti-Patterns to Avoid

- **Wizard as page navigation:** Do not use `INavigationService` for wizard steps — it replaces the whole page. Use step index + `IsVisible` inside the dialog.
- **Blocking UI thread during port probe:** `FindFreePort` is fast (network stack), but call it in the ViewModel constructor or a background task, not synchronously in the Avalonia UI thread if there is any doubt.
- **Re-parsing windrose_plus.json for opt-in state:** AppSettings is the app-side mirror; do not read the JSON file to determine opt-in state — only write it at install time.
- **Singleton InstallWizardViewModel:** Must be Transient — each wizard open needs a fresh state (fresh password, fresh port probe).
- **x:Name field access in new dialogs:** Avalonia source generators do not reliably emit x:Name fields for elements added after the initial class generation. Use `FindControl<T>("name")` as established in Phase 08-03 decision for `AboutDialog`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Password masking UI | Custom PasswordBox overlay | Avalonia `TextBox` with `PasswordChar="•"` + eye-toggle swapping `PasswordChar` to `""` | Avalonia has no native PasswordBox; this toggle pattern is the standard Avalonia approach |
| Copy to clipboard | Custom Win32 call | `TopLevel.GetTopLevel(control)?.Clipboard?.SetTextAsync(text)` | Avalonia's cross-platform clipboard API |
| URL open | ShellExecute P/Invoke | `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })` | Already used in codebase (DashboardViewModel, AboutDialog) |
| Cryptographic randomness | Custom PRNG | `RandomNumberGenerator.GetBytes` (BCL) | Already available, CSPRNG-grade |
| Free port detection | Raw socket bind | `TcpListener` start/stop (BCL) | Clean, reliable, no external deps |

---

## Common Pitfalls

### Pitfall 1: Avalonia PasswordBox — no native control
**What goes wrong:** Developers look for `PasswordBox` (WPF) in Avalonia and don't find it.
**Why it happens:** Avalonia uses `TextBox` with `PasswordChar` property instead.
**How to avoid:** Use `<TextBox PasswordChar="•" x:Name="RconPasswordBox" />`. Eye-toggle changes `PasswordChar` to `""` (reveal) or back to `"•"` (mask) from code-behind.
**Warning signs:** If you see a `PasswordBox` XAML type, it's a WPF import error.

### Pitfall 2: x:Name code-behind access unreliable for dynamically added elements
**What goes wrong:** `this.RconPasswordBox` is null at runtime even though the x:Name is set in AXAML.
**Why it happens:** Avalonia's source generator only emits fields for elements present in the initial compilation. See Phase 08-03 decision for `AboutDialog`.
**How to avoid:** Use `this.FindControl<TextBox>("RconPasswordBox")` consistently for all named controls in new dialogs.
**Warning signs:** NullReferenceException on first access in dialog constructor or Loaded event.

### Pitfall 3: OptInState migration not idempotent
**What goes wrong:** Re-running migration on v1.2+ resets `OptedOut` servers back to `NeverAsked`.
**Why it happens:** Migration iterates all servers in `WindrosePlusActiveByServer` unconditionally.
**How to avoid:** Only seed `NeverAsked` when the key is absent from `WindrosePlusOptInStateByServer` (use `ContainsKey` check before setting).
**Warning signs:** Users report that "Not now" decisions don't persist across restarts.

### Pitfall 4: DashboardViewModel timer fires before migration completes
**What goes wrong:** `RetrofitBannerVisible` computes `NeverAsked` for all servers on first tick, even if `AppSettingsService.LoadAsync` hasn't completed migration yet.
**Why it happens:** The 2s timer starts in `DashboardViewModel` constructor immediately.
**How to avoid:** Run migration inside `AppSettingsService.LoadAsync` (synchronous, before `OnFrameworkInitializationCompleted` returns). Migration is complete before any VM is constructed.

### Pitfall 5: Port probe in wrong thread
**What goes wrong:** `TcpListener.Start()` blocks UI thread briefly; worse, it throws `SocketException` that is swallowed.
**Why it happens:** Port probe called synchronously in ViewModel constructor on UI thread.
**How to avoid:** Wrap port probe in `await Task.Run(FindFreePort)` and call from an async init method, not the constructor. Assign result to `[ObservableProperty] int _dashboardPort`.

### Pitfall 6: windrose_plus.json write fails silently
**What goes wrong:** Install completes but WindrosePlus cannot authenticate (RCON password not in JSON).
**Why it happens:** `windrose_plus.json` write is not checked for success; file may not exist yet or server dir may be read-only.
**How to avoid:** In `IWindrosePlusService.InstallAsync` (or a new `ConfigureAsync` method called after install), explicitly write and verify the file. Log at `Warning` level if write fails. Toast the user.

### Pitfall 7: Retrofit banner shown during active install
**What goes wrong:** User sees the retrofit banner while the wizard is running, confusing state.
**Why it happens:** `DashboardViewModel.RefreshAsync` fires every 2s regardless of wizard state.
**How to avoid:** After "Install WindrosePlus" is clicked in the banner, immediately set `RetrofitBannerVisible = false` in the ViewModel before opening `RetrofitDialog`. Re-evaluate on next refresh only after install completes.

---

## Code Examples

### InstallWizardWindow AXAML skeleton

```xml
<!-- Source: ConfirmDialog.axaml + AboutDialog.axaml patterns -->
<Window xmlns="https://github.com/avaloniaui"
        Width="560" SizeToContent="Height" CanResize="False"
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False"
        Background="{DynamicResource BrandNavySurfaceBrush}"
        Title="{DynamicResource Wizard.Title}">
    <Border CornerRadius="8" Padding="24"
            BorderBrush="{DynamicResource BrandNavyBorderBrush}" BorderThickness="1"
            Background="{DynamicResource BrandNavySurfaceBrush}">
        <StackPanel Spacing="24">
            <!-- Stepper header (3 circles + labels) -->
            <!-- Step content zone (IsVisible per CurrentStep) -->
            <!-- Footer (Back / Next / Install / Cancel) -->
        </StackPanel>
    </Border>
</Window>
```

### Retrofit Banner (inline in DashboardView.axaml)

```xml
<!-- Source: DashboardView.axaml crash-warning pattern (same Grid 2-col layout) -->
<Border Classes="card" Margin="0,0,0,16"
        IsVisible="{Binding RetrofitBannerVisible}"
        BorderBrush="{DynamicResource BrandWarningBrush}" BorderThickness="1">
    <Grid ColumnDefinitions="*,Auto">
        <StackPanel Grid.Column="0" Spacing="6">
            <TextBlock Classes="section-header"
                       Text="{DynamicResource Retrofit.Banner.Title}" />
            <TextBlock Classes="body muted" TextWrapping="Wrap"
                       Text="{DynamicResource Retrofit.Banner.Body}" />
        </StackPanel>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8"
                    VerticalAlignment="Center">
            <Button Classes="primary"
                    Content="{DynamicResource Retrofit.Banner.Action.Install}"
                    Command="{Binding OpenRetrofitDialogCommand}" />
            <Button Classes="subtle"
                    Content="{DynamicResource Retrofit.Banner.Action.Later}"
                    Command="{Binding DismissRetrofitBannerCommand}" />
        </StackPanel>
    </Grid>
</Border>
```

### OptInState Enum and AppSettings Extension

```csharp
// In WindroseServerManager.Core/Models/AppSettings.cs
public enum OptInState { NeverAsked, OptedIn, OptedOut }

// Four new dicts in AppSettings class:
public Dictionary<string, string> WindrosePlusRconPasswordByServer { get; set; } = new();
public Dictionary<string, int>    WindrosePlusDashboardPortByServer { get; set; } = new();
public Dictionary<string, string> WindrosePlusAdminSteamIdByServer  { get; set; } = new();
public Dictionary<string, OptInState> WindrosePlusOptInStateByServer { get; set; } = new();
```

### ServerInstallInfo Record Extension

```csharp
// WindroseServerManager.Core/Models/ServerInstallInfo.cs
public sealed record ServerInstallInfo(
    bool IsInstalled,
    string InstallDir,
    string? BuildId,
    long SizeBytes,
    DateTime? LastUpdatedUtc,
    bool WindrosePlusActive = false,
    string? WindrosePlusVersionTag = null,
    // Phase 9 additions:
    string? WindrosePlusRconPassword = null,
    int WindrosePlusDashboardPort = 0,
    string? WindrosePlusAdminSteamId = null,
    OptInState WindrosePlusOptInState = OptInState.NeverAsked)
{
    public static ServerInstallInfo NotInstalled(string installDir) =>
        new(false, installDir, null, 0, null);
}
```

### DI Registration additions (App.axaml.cs)

```csharp
// After existing s.AddSingleton<IWindrosePlusService, WindrosePlusService>();
s.AddTransient<InstallWizardViewModel>();
s.AddTransient<RetrofitBannerViewModel>();
// DashboardViewModel already Singleton — gains IWindrosePlusService dep
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| InstallationView does everything (path + install trigger) | Wizard handles new-install; InstallationView handles re-install / update | Clean separation; wizard can collect secrets before SteamCMD starts |
| No WindrosePlus opt-in state | `OptInState` tri-state enum persisted per-server | Enables retrofit "never ask again" and deferred CTA from Phase 12 |
| AppSettings has 2 WP dicts | AppSettings has 6 WP dicts (Phase 8 + 4 new) | All WP state centralized; no double-parse of windrose_plus.json |

---

## Open Questions

1. **windrose_plus.json write location and schema**
   - What we know: `WindrosePlusService.UserOwnedRelativePaths` preserves `windrose_plus.json` during install; the `project_windroseplus_maintainer_agreement` memory confirms `windrose_plus.json[RCON]` is the key.
   - What's unclear: Exact JSON schema (field names for RCON password, port, admin SteamID). Need to read `windrose_plus.json` from a real WindrosePlus install or read the upstream README.
   - Recommendation: Read `https://github.com/HumanGenome/WindrosePlus` README during implementation to confirm field names before writing. Use `JsonSerializer` with `WriteIndented=true`. If schema unknown, write a minimal JSON: `{ "RCON": { "Password": "...", "Port": 18080, "AdminSteamId": "..." } }`.

2. **`IServerInstallService` API for new-server install from wizard**
   - What we know: `InstallationViewModel` calls `_install` for SteamCMD installs. `InstallWizardViewModel` must call the same service.
   - What's unclear: Whether the existing `InstallAsync` method on `IServerInstallService` accepts a target directory override, or whether it always reads from AppSettings.
   - Recommendation: Before Plan 09-01, read `IServerInstallService` and `ServerInstallService` to confirm parameter signature. If `InstallDir` must be set in AppSettings first, the wizard must write `settings.Current.ServerInstallDir = wizardVm.InstallDir` before calling install.

3. **AppSettings save mechanism**
   - What we know: `IAppSettingsService.SaveAsync()` exists (called in `SettingsViewModel`). Migration should write 4 new dict keys.
   - What's unclear: Whether `AppSettingsService` handles missing JSON keys gracefully on upgrade (i.e., missing dict = empty dict, not null crash).
   - Recommendation: Confirm `AppSettingsService` uses `JsonSerializerOptions` with `DefaultValueHandling` or equivalent so missing dict keys in old JSON files are initialized to `new()` — not null. Add null-coalescing guards in migration if needed.

---

## Validation Architecture

The project has an xUnit test project. Phase 9 introduces new logic units that are independently testable.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | existing test project |
| Quick run command | `dotnet test --filter "Category=Phase9" --no-build` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| WIZARD-03 | RCON password ≥24 chars, URL-safe chars only | unit | `dotnet test --filter "GenerateRconPassword"` | ❌ Wave 0 |
| WIZARD-03 | Steam-ID regex accepts valid SteamID64 | unit | `dotnet test --filter "SteamIdParsing"` | ❌ Wave 0 |
| WIZARD-03 | Steam-ID regex accepts numeric profile URL | unit | `dotnet test --filter "SteamIdParsing"` | ❌ Wave 0 |
| WIZARD-03 | Steam-ID regex rejects vanity URL | unit | `dotnet test --filter "SteamIdParsing"` | ❌ Wave 0 |
| WIZARD-04 | Free-port probe returns port in 18080–18099 | unit | `dotnet test --filter "FindFreePort"` | ❌ Wave 0 |
| RETRO-01 | Migration seeds NeverAsked for existing servers | unit | `dotnet test --filter "OptInMigration"` | ❌ Wave 0 |
| RETRO-01 | Migration is idempotent (does not overwrite OptedOut) | unit | `dotnet test --filter "OptInMigration"` | ❌ Wave 0 |
| RETRO-03 | "Not now" sets OptedOut, does not install | manual smoke | — | — |

### Wave 0 Gaps

- [ ] `tests/WindroseServerManager.Tests/Phase9/RconPasswordGeneratorTests.cs` — covers WIZARD-03 (password generation)
- [ ] `tests/WindroseServerManager.Tests/Phase9/SteamIdParserTests.cs` — covers WIZARD-03 (Steam-ID validation)
- [ ] `tests/WindroseServerManager.Tests/Phase9/FreePortProbeTests.cs` — covers WIZARD-04
- [ ] `tests/WindroseServerManager.Tests/Phase9/OptInMigrationTests.cs` — covers RETRO-01

---

## Sources

### Primary (HIGH confidence)

- Direct codebase inspection — `ConfirmDialog.axaml`, `AboutDialog.axaml`, `DashboardView.axaml`, `DashboardViewModel.cs`, `InstallationView.axaml`, `InstallationViewModel.cs`, `App.axaml.cs`, `AppSettings.cs`, `ServerInstallInfo.cs`, `IWindrosePlusService.cs`, `WindrosePlusService.cs`, `InstallProgress.cs`
- `.planning/phases/09-opt-in-ux-wizard-retrofit/09-CONTEXT.md` — all locked decisions
- `.planning/phases/09-opt-in-ux-wizard-retrofit/09-UI-SPEC.md` — visual contract
- `.planning/STATE.md` — Phase 08 accumulated decisions

### Secondary (MEDIUM confidence)

- `project_windroseplus_maintainer_agreement` memory — `windrose_plus.json[RCON]` schema key confirmed
- Avalonia `TextBox.PasswordChar` behavior — known from Avalonia docs pattern (no PasswordBox type in Avalonia; TextBox with PasswordChar is the canonical approach)

### Tertiary (LOW confidence — flag for validation)

- windrose_plus.json exact field names for Port and AdminSteamId — inferred from RCON key; exact schema not inspected from a live install

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all BCL + existing deps
- Architecture patterns: HIGH — directly derived from existing codebase dialog patterns
- Pitfalls: HIGH — Avalonia-specific (x:Name, PasswordChar) confirmed from Phase 08 decisions and Avalonia's known API
- windrose_plus.json schema: LOW — exact field names for Port/AdminSteamId not confirmed

**Research date:** 2026-04-19
**Valid until:** 2026-05-19 (stable Avalonia API, no fast-moving dependencies)
