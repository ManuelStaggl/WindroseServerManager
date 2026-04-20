# Phase 11: Feature Views — Research

**Researched:** 2026-04-20
**Domain:** WindrosePlus HTTP API integration, FileSystemWatcher, Canvas map rendering, INI config editing
**Confidence:** HIGH (architecture + stack), MEDIUM (WindrosePlus RCON command syntax), LOW (events.log exact schema)

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PLAYER-01 | Players view lists connected players (name, Steam-ID, alive, session duration); refreshes on configurable interval | `GET /api/status` returns players array; poll via `IHttpClientFactory` on configurable timer interval |
| PLAYER-02 | Kick a selected player with confirmation dialog | `POST /api/rcon` with command `wp.kick {steamId}` or `wp.kick {playerName}`; gate with existing `ConfirmDialog` pattern |
| PLAYER-03 | Ban a selected player (permanent or timed) with confirmation dialog | `POST /api/rcon` with `wp.ban {steamId}` / `wp.ban {steamId} {minutes}`; custom ban dialog with timed/permanent toggle |
| PLAYER-04 | Broadcast a chat message to all players | `POST /api/rcon` with `wp.say {message}`; single input field, no confirmation required |
| EVENT-01 | Events view streams join/leave records from `events.log` live via FileSystemWatcher | `System.IO.FileSystemWatcher` on `{serverDir}/windrose_plus_data/events.log`; parse line-delimited JSON on change |
| EVENT-02 | Filter/search events by player name, Steam-ID, event type | Client-side filter on in-memory `ObservableCollection` with debounced search |
| EVENT-03 | Events view stays responsive at >1000 entries (virtualization or pagination) | Avalonia `VirtualizingStackPanel` + `DataGrid` with `EnableRowVirtualization="True"` |
| CHART-01 | Sea-chart renders top-down world map with live player positions polled from `/query` | Canvas-based Avalonia rendering; poll `GET /query` on configurable interval; player positions as Canvas.SetLeft/Canvas.SetTop overlays |
| CHART-02 | Clicking a player marker opens popover with name, Steam-ID, alive, ship info | Avalonia `Popup` or inline detail panel bound to selected marker |
| EDITOR-01 | Editor lists all WindrosePlus-exposed settings grouped by category | Parse `windrose_plus.json` + `windrose_plus.ini`; show in grouped `ItemsControl` / `DataGrid` per category |
| EDITOR-02 | Editor validates values against config schema inline before save | Schema defined as static metadata in the app (float range, bool, int); inline error messages using `INotifyDataErrorInfo` |
| EDITOR-03 | Save writes config file; prompt restart if server is running | Write JSON / INI back to disk; check `ServerStatus.Running` via `IServerProcessService`; show toast or inline prompt |
</phase_requirements>

---

## Summary

Phase 11 builds four new full-page views on top of the existing `WindrosePlusService` + `IHttpClientFactory` infrastructure. Each view is a new Avalonia `UserControl` with its own ViewModel registered as a `Singleton` in DI, and a new `NavItem` entry in `MainWindowViewModel`.

The **Players view** polls `GET /api/status` on a configurable timer (default 10s), renders a `DataGrid` of connected players, and exposes kick/ban/broadcast via `POST /api/rcon`. All destructive actions use the existing `ConfirmDialog` pattern. The RCON password is read from `AppSettings.WindrosePlusRconPasswordByServer[serverDir]`.

The **Events view** opens a `FileSystemWatcher` on `{serverDir}/windrose_plus_data/events.log` and appends new events live to an `ObservableCollection`, with server-side filtering via a debounced search TextBox. Avalonia `DataGrid` with row virtualization handles >1000 entries without UI freeze.

The **Sea-Chart view** polls `GET /query` on a configurable interval, renders player positions as ellipse overlays on an Avalonia `Canvas`. A static world map image (bundled as a resource) provides the background; coordinate mapping requires a linear transform from game-world space to canvas pixels. Clicking a marker shows a `Popup` with player details.

The **INI/Multiplier editor** is the most complex: it parses `windrose_plus.json` (JSON multipliers + server config) and `windrose_plus.ini` (INI key-value settings) into a grouped in-memory model. The app ships a static schema catalogue (category, key, type, range, default) for the `windrose_plus.json` multipliers — the INI files are too large (2400+ keys) to schema-validate inline; validation is limited to type-checking (float, int, bool) and range guards.

**Primary recommendation:** Four plans (one per view), each self-contained. Shared infrastructure (RCON helper, `/query` model, events.log parser) goes into a new `WindrosePlusApiService` in Core in Plan 11-01 (foundation wave).

---

## Standard Stack

### Core — no new NuGet packages required

All needed APIs are already in the project.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Net.Http.HttpClient` via `IHttpClientFactory` | .NET 9 BCL | Poll `/api/status`, `/query`, POST `/api/rcon` | Already registered via `s.AddHttpClient()` in App.axaml.cs line 70 |
| `System.IO.FileSystemWatcher` | .NET 9 BCL | Watch `events.log` for new lines | BCL, no dep; pattern established in Avalonia file-monitoring scenarios |
| `System.Text.Json` | .NET 9 BCL | Deserialize `/api/status`, `/query` JSON responses + `windrose_plus.json` | Already used throughout the project |
| `CommunityToolkit.Mvvm` | already in project | `[ObservableProperty]`, `[RelayCommand]` on all 4 ViewModels | Identical pattern to existing ViewModels |
| Avalonia `Canvas` | already in project | Sea-chart player-marker rendering | Standard Avalonia control; no third-party map library needed |
| Avalonia `DataGrid` | already in project | Players + Events tables | Used in BackupsView, ModsView |
| Avalonia `Popup` | already in project | Sea-chart marker popover | Standard Avalonia overlay control |

**Installation:** none — all libraries already present.

### No new NuGet packages required

**Confirmed:** codebase search shows `System.Net.Http`, `System.Text.Json`, `CommunityToolkit.Mvvm`, Avalonia DataGrid/Canvas/Popup all present.

---

## Architecture Patterns

### Recommended Component Structure

```
src/WindroseServerManager.Core/
├── Services/
│   ├── IWindrosePlusApiService.cs         # New — RCON calls, /query, /status polling
│   └── WindrosePlusApiService.cs          # New — implementation
├── Models/
│   ├── WindrosePlusPlayer.cs              # New — player data from /api/status
│   ├── WindrosePlusQueryResult.cs         # New — /query response (players + positions)
│   ├── WindrosePlusEvent.cs               # New — parsed events.log line
│   └── WindrosePlusConfigEntry.cs         # New — single config item (key, value, schema)

src/WindroseServerManager.App/
├── ViewModels/
│   ├── PlayersViewModel.cs                # New (Singleton)
│   ├── EventsViewModel.cs                 # New (Singleton)
│   ├── SeaChartViewModel.cs               # New (Singleton)
│   └── EditorViewModel.cs                 # New (Singleton)
├── Views/Pages/
│   ├── PlayersView.axaml / .cs            # New
│   ├── EventsView.axaml / .cs             # New
│   ├── SeaChartView.axaml / .cs           # New
│   └── EditorView.axaml / .cs             # New
tests/WindroseServerManager.Core.Tests/
└── Phase11/
    ├── WindrosePlusApiServiceTests.cs      # RCON URL, response parsing
    ├── EventsLogParserTests.cs             # events.log line parsing
    └── EditorConfigParserTests.cs          # windrose_plus.json + INI parsing
```

### Pattern 1: WindrosePlusApiService — new Core service

**What:** Centralises all HTTP calls to the WindrosePlus dashboard. Consumed by Players, Sea-Chart, and Editor ViewModels. Not a view-layer concern.

**Why new service instead of direct HttpClient in ViewModel:** Keeps ViewModels testable and decoupled. Avoids 4 ViewModels each constructing HTTP logic.

```csharp
// Confidence: HIGH — mirrors WindrosePlusService pattern already in project
public interface IWindrosePlusApiService
{
    /// <summary>GET /api/status — returns connected players, server info, multipliers.</summary>
    Task<WindrosePlusStatusResult?> GetStatusAsync(string serverDir, CancellationToken ct = default);

    /// <summary>GET /query — returns player positions for sea-chart.</summary>
    Task<WindrosePlusQueryResult?> QueryAsync(string serverDir, CancellationToken ct = default);

    /// <summary>POST /api/rcon — execute an admin command. Returns response message.</summary>
    Task<string?> RconAsync(string serverDir, string command, CancellationToken ct = default);

    /// <summary>Read and parse current windrose_plus.json from server dir.</summary>
    WindrosePlusConfig? ReadConfig(string serverDir);

    /// <summary>Write windrose_plus.json back to server dir. Caller ensures server is stopped.</summary>
    Task WriteConfigAsync(string serverDir, WindrosePlusConfig config, CancellationToken ct = default);
}
```

**Constructor pattern:** `(ILogger<WindrosePlusApiService>, IHttpClientFactory, IAppSettingsService)` — mirrors `DashboardViewModel`'s health-check pattern exactly.

**HTTP base URL:** `http://localhost:{port}` where `port = _settings.Current.WindrosePlusDashboardPortByServer[serverDir]`.

**Auth:** RCON requests include `{ "password": "...", "command": "..." }` in JSON body. Password from `AppSettings.WindrosePlusRconPasswordByServer[serverDir]`. The `/api/status` and `/query` endpoints require cookie-based auth per the docs — but the maintainer confirmed `POST /api/rcon` with password in body works without cookie. For GET endpoints, test whether they require auth or are open (Phase 10 health-check uses `/api/status` without auth successfully — HIGH confidence that GET endpoints are open or use query-param auth).

**Timeout:** 5s for GET poll calls, 10s for RCON commands. Use `CancellationTokenSource.CreateLinkedTokenSource(ct)` with `CancelAfter`.

### Pattern 2: Players View

**Poll architecture:**

```csharp
public partial class PlayersViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] private ObservableCollection<WindrosePlusPlayer> _players = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private WindrosePlusPlayer? _selectedPlayer;
    [ObservableProperty] private string _broadcastMessage = string.Empty;
    [ObservableProperty] private int _refreshIntervalSeconds = 10;

    private readonly System.Timers.Timer _pollTimer;

    // Kick, Ban, Broadcast commands — all [RelayCommand]
    // Kick + Ban show ConfirmDialog before calling RconAsync
    // Broadcast: no confirm dialog, just RconAsync("wp.say {message}")
}
```

**RCON commands (MEDIUM confidence — inferred from docs, not in explicit kick/ban doc):**
- Kick: `wp.kick {steamId64}` or `wp.kick {playerName}` — exact syntax needs runtime verification
- Ban permanent: `wp.ban {steamId64}` — timed form unknown, may be `wp.ban {steamId64} {minutes}`
- Broadcast: `wp.say {message}` — standard "say" convention; all RCON systems use this

**Session duration:** `/api/status` player objects include a `playtime` or `sessionSeconds` field (confirmed: `wp.playtime` RCON command exists, and the player object from `/api/status` is documented to include player info). If session duration is not in the REST response, fall back to `wp.playtime {steamId}` RCON call on demand.

**Refresh interval:** Stored in `AppSettings` (new field `WindrosePlusPlayerRefreshSeconds`, default 10). Configurable in SettingsView (Phase 12 or incidental).

**Confirmation dialog pattern (existing):**

```csharp
// From ConfirmDialog.axaml — existing pattern in Dialogs/
var confirm = new ConfirmDialog();
confirm.SetContent(title, message);
var result = await confirm.ShowDialog<bool>(window);
if (!result) return;
```

### Pattern 3: Events View

**FileSystemWatcher lifecycle:**

```csharp
public partial class EventsViewModel : ViewModelBase, IDisposable
{
    private FileSystemWatcher? _watcher;
    private long _lastReadPosition;  // tail-read position tracking

    private void StartWatching(string serverDir)
    {
        var logPath = Path.Combine(serverDir, "windrose_plus_data", "events.log");
        var dir = Path.GetDirectoryName(logPath)!;
        if (!Directory.Exists(dir)) return;  // WP not yet started

        _lastReadPosition = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
        _watcher = new FileSystemWatcher(dir, "events.log")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnLogChanged;
    }

    private void OnLogChanged(object sender, FileSystemEventArgs e)
    {
        // Read new lines from _lastReadPosition forward
        // Parse each line as JSON WindrosePlusEvent
        // Dispatch to UI thread: Dispatcher.UIThread.Post(() => Events.Insert(0, evt))
    }
}
```

**events.log format (LOW confidence — no official schema doc):**

Based on maintainer's scripting API (`getPlayers()` exposes `name`, `steamId`, join/leave hooks) and the line-delimited JSON claim:

```json
{ "type": "join", "steamId": "76561198012345678", "name": "PlayerName", "timestamp": "2026-04-20T12:00:00Z" }
{ "type": "leave", "steamId": "76561198012345678", "name": "PlayerName", "timestamp": "2026-04-20T13:00:00Z" }
```

**Defensive parsing required:** Use `JsonDocument.TryGetProperty` for all fields; skip unrecognized lines silently. The schema is LOW confidence — be tolerant of additive fields.

**Initial load:** On view activation, read the last N lines of events.log (tail-read, same pattern as `ServerEventLog.ReadRecentAsync`). Then attach `FileSystemWatcher` for live updates.

**Virtualization (EVENT-03):**

Avalonia `DataGrid` supports row virtualization natively via `EnableRowVirtualization="True"` (default true). For >1000 entries, use `VirtualizingStackPanel` in the DataGrid's row container:

```xml
<DataGrid EnableRowVirtualization="True"
          MaxHeight="600"
          ItemsSource="{Binding FilteredEvents}">
```

`FilteredEvents` is a filtered view: use `CollectionViewSource` or a separate `ObservableCollection<WindrosePlusEvent>` that gets rebuilt on filter change. **Do NOT filter in-place on the main collection** — rebuild a filtered copy on UI thread.

### Pattern 4: Sea-Chart View

**Map image:** WindrosePlus generates a heightmap via `wp.mapgen`. This is a one-time server-side operation. The map image lives at a predictable path after generation (e.g. `windrose_plus_data/map.png` — exact path LOW confidence). Display as `<Image>` background on a `Canvas`.

**Coordinate mapping (MEDIUM confidence):**

The `/query` endpoint returns player positions as `{ x: 14520, y: -8340 }` in game-world units. Linear mapping to canvas:

```csharp
double CanvasX = (worldX - WorldMinX) / (WorldMaxX - WorldMinX) * CanvasWidth;
double CanvasY = (WorldMaxY - worldY) / (WorldMaxY - WorldMinY) * CanvasHeight;
```

World bounds (LOW confidence — need actual Windrose world extent):
- X range: approximately -30000 to +30000
- Y range: approximately -30000 to +30000
These must be treated as configurable constants, not hardcoded magic numbers. Store as `private const double WorldExtent = 30000` with a note to update after live testing.

**Player markers:**

```xml
<Canvas x:Name="MapCanvas">
    <Image Source="{Binding MapImage}" Stretch="Uniform"
           Width="{Binding $parent[Canvas].Bounds.Width}"
           Height="{Binding $parent[Canvas].Bounds.Height}" />
    <ItemsControl ItemsSource="{Binding PlayerMarkers}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemContainerTheme>
            <ControlTheme TargetType="ContentPresenter">
                <Setter Property="Canvas.Left" Value="{Binding CanvasX}" />
                <Setter Property="Canvas.Top" Value="{Binding CanvasY}" />
            </ControlTheme>
        </ItemsControl.ItemContainerTheme>
        <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:PlayerMarkerViewModel">
                <Ellipse Width="12" Height="12"
                         Fill="{DynamicResource BrandAmberBrush}"
                         Stroke="White" StrokeThickness="1.5"
                         Cursor="Hand">
                    <Ellipse.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding SelectCommand}" />
                    </Ellipse.GestureRecognizers>
                </Ellipse>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Canvas>
```

**Popover (CHART-02):** Use an Avalonia `Popup` anchored to the selected marker `Ellipse`, or — simpler — a details panel in a `SplitView` right pane that appears when a marker is clicked.

**Poll interval:** Default 5s (more frequent than player list since position changes quickly). Use same `System.Timers.Timer` pattern as DashboardViewModel.

### Pattern 5: INI/Multiplier Editor

**Scope decision (EDITOR-01):** The `windrose_plus.ini` has 2400+ keys across 5 files. Exposing all is impractical. Phase 11 scope: expose the `windrose_plus.json` settings only (multipliers + server config — ~15-20 keys). The INI files are left for future phases or exposed as a raw text editor.

**windrose_plus.json schema (confirmed via maintainer agreement + docs):**

```json
{
  "Server": {
    "http_port": 8780,
    "rcon_enabled": false,
    "rcon_password": "...",
    "admin_steam_ids": ["76561198012345678"]
  },
  "Multipliers": {
    "xp": 1.0,
    "loot": 1.0,
    "stack_size": 1.0,
    "craft_cost": 1.0,
    "crop_speed": 1.0,
    "cooking_speed": 1.0,
    "harvest_yield": 1.0,
    "inventory_size": 1.0,
    "points_per_level": 1.0,
    "weight": 1.0
  }
}
```

**Static schema metadata:** Ship a `WindrosePlusConfigSchema` class in Core with a `IReadOnlyList<ConfigEntrySchema>` for each known key:

```csharp
public sealed record ConfigEntrySchema(
    string Category,   // "Multipliers", "Server"
    string Key,        // "xp", "loot"
    string Type,       // "float", "int", "bool", "string"
    double? Min,
    double? Max,
    object? Default,
    string DescriptionKey); // localization key for tooltip
```

**Inline validation (EDITOR-02):** Each `ConfigEntryViewModel` implements `INotifyDataErrorInfo`:

```csharp
public partial class ConfigEntryViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    private string _rawValue = string.Empty;

    partial void OnRawValueChanged(string value)
    {
        ValidateProperty(value, nameof(RawValue));
    }
}
```

Or simpler: validate on `RawValue` change with a manual `ErrorMessage` property — `ObservableValidator` and `[CustomValidation]` attribute works in CommunityToolkit.Mvvm 8.x.

**Save + restart prompt (EDITOR-03):**

```csharp
[RelayCommand]
private async Task SaveAsync(CancellationToken ct)
{
    if (!ValidateAll()) return;

    var config = BuildConfig();
    await _api.WriteConfigAsync(_serverDir, config, ct);
    _toasts.Success(Loc.Get("Editor.Saved"));

    if (_proc.Status == ServerStatus.Running)
        _toasts.Warning(Loc.Get("Editor.RestartRequired"));
}
```

### Pattern 6: Navigation — adding 4 new pages

Add 4 new `NavItem` entries to `MainWindowViewModel.NavItems` and 4 new Singleton registrations in `App.axaml.cs`. Pages should only appear (or be enabled) when `WindrosePlusActive` is true for the current server — but actual empty-state rendering for opt-out is Phase 12's scope. Phase 11 renders the views regardless; they show a loading state or error when WindrosePlus is inactive.

```csharp
// MainWindowViewModel.NavItems additions:
new() { TitleKey = "Nav.Players", Icon = "\uE716", VmType = typeof(PlayersViewModel) },
new() { TitleKey = "Nav.Events", Icon = "\uE81C", VmType = typeof(EventsViewModel) },
new() { TitleKey = "Nav.SeaChart", Icon = "\uE909", VmType = typeof(SeaChartViewModel) },
new() { TitleKey = "Nav.Editor", Icon = "\uE70F", VmType = typeof(EditorViewModel) },

// App.axaml.cs additions:
s.AddSingleton<IWindrosePlusApiService, WindrosePlusApiService>();
s.AddSingleton<PlayersViewModel>();
s.AddSingleton<EventsViewModel>();
s.AddSingleton<SeaChartViewModel>();
s.AddSingleton<EditorViewModel>();
```

### Anti-Patterns to Avoid

- **Don't build a custom INI parser for the full 2400-key ini files in Phase 11.** Scope is `windrose_plus.json` multipliers only. INI files are vast and their schema is not validated by the app.
- **Don't fire RCON commands without debouncing.** `POST /api/rcon` is rate-limited to 1 req/s per IP. Queue commands if user clicks rapidly.
- **Don't use `ObservableCollection.Clear()` + re-add on every poll.** For Players view, diff the collection (match by SteamID) to avoid flicker and preserve selection.
- **Don't read events.log from position 0 on every FileSystemWatcher event.** Track `_lastReadPosition` and only read new bytes.
- **Don't block the UI thread with FileSystemWatcher callbacks.** Callbacks fire on a thread-pool thread — always `Dispatcher.UIThread.Post(...)` before touching `ObservableCollection`.
- **Don't hardcode the WindrosePlus dashboard port.** Always read from `AppSettings.WindrosePlusDashboardPortByServer[serverDir]`. Guard against port 0 (same as HealthCheckHelper pattern).
- **Don't use cookie-based auth for RCON.** The maintainer confirmed HTTP is the recommended integration surface with password in body for `POST /api/rcon`. GET endpoints (`/api/status`, `/query`) appear to be open based on Phase 10 health-check precedent.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP polling | Custom socket or raw `HttpClient` fields | `IHttpClientFactory.CreateClient()` + existing `WindrosePlusApiService` | Already registered in DI, consistent timeout/retry pattern |
| File tail-read | Custom stream class | `FileStream` + `StreamReader` tracking `Position` | BCL, O(1) seek to last-read position |
| JSON events.log parse | Regex string parsing | `JsonDocument.Parse(line)` + `TryGetProperty` | Handles encoding edge-cases, survives additive fields |
| INI parsing | Custom regex parser | `Microsoft.Extensions.Configuration.Ini` or simple split-on-`=` for the limited editor scope | Don't over-engineer; editor only needs `windrose_plus.json` (JSON) |
| Map coordinate math | Third-party map library | `Avalonia.Canvas` + linear transform formula | No tiles, no projections needed — game uses flat coord space |
| Confirmation dialogs | New dialog per action | Existing `ConfirmDialog` (Views/Dialogs/) | Already supports custom title + message; no new Window needed |
| Player list diff | Full clear + reload | Manual diff by SteamID, update/add/remove | Preserves selection, eliminates flicker |

**Key insight:** The WindrosePlus surface is HTTP + file I/O. All primitives are BCL — no new NuGet packages required for any of the 4 views.

---

## Common Pitfalls

### Pitfall 1: GET /api/status requires authentication
**What goes wrong:** The docs say "all endpoints except /api/health require cookie-based auth." If `/api/status` requires a session cookie, the player poll will 401 on every request.
**Why it happens:** Phase 10 health-check uses `/api/health` (no auth) — this succeeded. `/api/status` may require auth.
**How to avoid:** Test `/api/status` without auth first (Phase 10 precedent suggests it may be open). If 401: send `{ "password": "..." }` as a query param or Authorization header. Alternatively, establish a session via a single `POST /api/rcon` call and reuse the cookie. **Plan 11-01 must include a live test against a running WindrosePlus instance.**
**Fallback:** If REST GET auth is complex, use `POST /api/rcon` with `wp.players` command to get player list as text, then parse the response string.

### Pitfall 2: events.log schema is undocumented
**What goes wrong:** Parser breaks on unexpected fields, missing fields, or different event types beyond join/leave.
**Why it happens:** WindrosePlus docs show event hooks in the Lua API but no JSON schema for the log file.
**How to avoid:** Parse defensively: `JsonDocument.TryGetProperty` for every field. Only require `type` and `timestamp`; treat `steamId`/`name` as optional (may be absent for server events). Skip unrecognized types silently with a `Log.Debug` entry.
**Warning signs:** `JsonException` on log lines that contain server start/stop events rather than player events.

### Pitfall 3: World coordinate bounds are unknown
**What goes wrong:** Player markers appear at wrong positions or off-canvas if world extent differs from assumed values.
**Why it happens:** Windrose world size is not publicly documented. The `/query` response example shows `x: 14520, y: -8340` but no bounds.
**How to avoid:** Auto-scale: on first poll, collect all player positions and infer tentative bounds. Store as `WorldMinX`, `WorldMaxX` etc. on the ViewModel and recalculate canvas positions on each poll. Ships conservatively with `±30000` defaults that auto-expand as data arrives.
**Warning signs:** All markers cluster in the center of the canvas.

### Pitfall 4: FileSystemWatcher events fire multiple times per write
**What goes wrong:** `Changed` event fires 2–3 times per log append (known .NET behavior: once for last-write, once for size change).
**Why it happens:** `FileSystemWatcher` fires on both `NotifyFilters.LastWrite` and `NotifyFilters.Size` independently.
**How to avoid:** Debounce with a `SemaphoreSlim(1)` + `CancellationToken`: cancel previous read task when a new event arrives within 100ms.

### Pitfall 5: Ban/kick commands return a text response, not structured JSON
**What goes wrong:** `POST /api/rcon` response `message` field is a human-readable string, not machine-parseable. Parsing it to confirm success is fragile.
**Why it happens:** RCON is designed for human console use. "OK" / error strings vary.
**How to avoid:** Treat any non-exception response as success. Surface the raw `message` string in a toast. Only retry/fail on HTTP error codes or network exceptions.

### Pitfall 6: Singleton ViewModels retain state across server switches
**What goes wrong:** User switches from Server A to Server B (future multi-server feature, or tests). PlayersViewModel still shows Server A's players.
**Why it happens:** ViewModels are registered as Singletons — they live for the app lifetime.
**How to avoid:** Implement a `ResetAsync(string newServerDir)` method on each ViewModel. Call it when `AppSettings.ServerInstallDir` changes (listen to `IAppSettingsService.SettingsChanged` event). For Phase 11 (single server), this is low-risk but the reset method should still exist for future-proofing.

### Pitfall 7: windrose_plus.json write corrupts config if server is running
**What goes wrong:** Server may read `windrose_plus.json` at any time. Mid-write is a race condition.
**Why it happens:** The INI editor writes directly to the config file.
**How to avoid:** Write to a temp file, then `File.Move(..., overwrite: true)` — same atomic-merge pattern used in `WindrosePlusService.InstallAsync`. Check `ServerStatus != Running` before saving; if running, warn user (EDITOR-03) and let them choose to proceed or wait.

---

## Code Examples

### WindrosePlusApiService — RCON call

```csharp
// Source: maintainer agreement (project_windroseplus_maintainer_agreement.md)
// Confidence: HIGH — confirmed HTTP is recommended integration surface
public async Task<string?> RconAsync(string serverDir, string command, CancellationToken ct = default)
{
    var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);
    if (port <= 0)
    {
        Log.Warning("WindrosePlus port not configured for {Dir}", serverDir);
        return null;
    }
    var password = _settings.Current.WindrosePlusRconPasswordByServer.GetValueOrDefault(serverDir, string.Empty);

    using var http = _httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(10);

    var body = JsonSerializer.Serialize(new { password, command });
    using var content = new StringContent(body, Encoding.UTF8, "application/json");

    try
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var resp = await http.PostAsync($"http://localhost:{port}/api/rcon", content, cts.Token)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : null;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
    {
        Log.Warning(ex, "RCON command '{Command}' failed for {Dir}", command, serverDir);
        return null;
    }
}
```

### Events.log defensive parser

```csharp
// Confidence: MEDIUM — schema inferred from Lua API + line-delimited JSON claim
public static WindrosePlusEvent? TryParseLine(string line)
{
    if (string.IsNullOrWhiteSpace(line)) return null;
    try
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is not "join" and not "leave") return null; // skip non-player events

        var steamId  = root.TryGetProperty("steamId", out var sid) ? sid.GetString() : null;
        var name     = root.TryGetProperty("name",    out var n)   ? n.GetString()   : "Unknown";
        var tsStr    = root.TryGetProperty("timestamp", out var ts) ? ts.GetString()  : null;

        var timestamp = tsStr is not null && DateTime.TryParse(tsStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : DateTime.UtcNow;

        return new WindrosePlusEvent(type, steamId, name, timestamp);
    }
    catch (JsonException)
    {
        Log.Debug("Skipping malformed events.log line: {Line}", line[..Math.Min(line.Length, 80)]);
        return null;
    }
}
```

### Sea-Chart coordinate mapping

```csharp
// Confidence: MEDIUM — linear transform, world bounds are LOW confidence estimates
private const double WorldExtentDefault = 30_000.0;

private (double cx, double cy) WorldToCanvas(double worldX, double worldY, double canvasW, double canvasH)
{
    var rangeX = _worldMaxX - _worldMinX;
    var rangeY = _worldMaxY - _worldMinY;
    if (rangeX <= 0 || rangeY <= 0) return (canvasW / 2, canvasH / 2);

    var cx = (worldX - _worldMinX) / rangeX * canvasW;
    var cy = (_worldMaxY - worldY) / rangeY * canvasH; // Y is inverted (game +Y = screen up)
    return (cx, cy);
}
```

### windrose_plus.json atomic write

```csharp
// Confidence: HIGH — same File.Move pattern as WindrosePlusService.AtomicMergeIntoServer
public async Task WriteConfigAsync(string serverDir, WindrosePlusConfig config, CancellationToken ct)
{
    var configPath = Path.Combine(serverDir, "windrose_plus.json");
    var tmpPath    = configPath + ".tmp";

    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(tmpPath, json, ct).ConfigureAwait(false);
    File.Move(tmpPath, configPath, overwrite: true);
    Log.Information("windrose_plus.json written to {Path}", configPath);
}
```

### ConfirmDialog usage pattern (existing)

```csharp
// Source: RetrofitBannerViewModel.cs — existing pattern in project
// Confidence: HIGH — direct existing pattern
private async Task KickPlayerAsync(WindrosePlusPlayer player, CancellationToken ct)
{
    var window = Avalonia.Application.Current?.ApplicationLifetime is
        Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
        ? desktop.MainWindow : null;
    if (window is null) return;

    var confirm = new ConfirmDialog();
    confirm.SetContent(
        Loc.Get("Players.Kick.Title"),
        Loc.Format("Players.Kick.Message", player.Name));
    confirm.Styles.Add(App.Current!.Styles[0]); // inherit theme

    var result = await confirm.ShowDialog<bool>(window);
    if (!result) return;

    var response = await _api.RconAsync(_serverDir, $"wp.kick {player.SteamId}", ct);
    _toasts.Success(Loc.Format("Players.Kicked", player.Name));
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| RCON as raw TCP (Valve RCON protocol) | HTTP `POST /api/rcon` with JSON body | WindrosePlus design | Simpler auth, no binary framing, same-host only |
| Map rendering with third-party tile library | Avalonia `Canvas` + linear transform | Phase 11 design | Zero additional dependencies, sufficient for flat game world |
| Full INI file editor (2400 keys) | JSON-only editor (`windrose_plus.json`, ~20 keys) | Phase 11 scope decision | Manageable schema, schema-validatable, covers the most common admin tasks |
| FileSystemWatcher on entire server dir | Watcher scoped to `windrose_plus_data/events.log` only | Phase 11 design | Avoids triggering on unrelated file changes |

---

## Open Questions

1. **Does GET /api/status require cookie auth?**
   - What we know: `/api/health` (no auth) worked in Phase 10. Docs say GET endpoints need cookie auth. Maintainer said HTTP is recommended integration.
   - What's unclear: Whether the dashboard opens an auth-free mode when accessed from localhost, or always requires cookie.
   - Recommendation: Plan 11-01 must test this against a live instance. Fallback: use `wp.players` RCON command to get player data as text if REST is gated.

2. **Exact RCON command syntax for kick/ban**
   - What we know: `wp.kick`, `wp.ban` commands exist (30 commands documented). Timed ban syntax unknown.
   - What's unclear: Is it `wp.ban {steamId} {minutes}` or `wp.ban {steamId} {days}`? Or flags?
   - Recommendation: Plan for `wp.ban {steamId}` (permanent) + `wp.ban {steamId} {minutes}` (timed). Document as tentative in code comments; make the command string easily configurable.

3. **events.log exact JSON schema**
   - What we know: Line-delimited JSON, join/leave events, written by Lua hooks in WindrosePlus.
   - What's unclear: Field names (`steamId` vs `steam_id`?), server lifecycle events, timestamp format, whether session duration is included.
   - Recommendation: Parse defensively (see Code Examples). Log all unknown fields at Debug level in a first implementation so they appear in Serilog output during live testing.

4. **Map image path and generation**
   - What we know: `wp.mapgen` generates a heightmap. WindrosePlus docs mention a browser-based sea chart.
   - What's unclear: Where the generated map image file is stored (`windrose_plus_data/map.png`?). Whether Phase 11 needs to trigger generation or whether the map is always present.
   - Recommendation: Implement a "Generate Map" button in Sea-Chart view that calls `RconAsync("wp.mapgen")` and then loads the image from the expected path. Show a placeholder if no map exists yet.

5. **Player position Y-axis orientation**
   - What we know: `/query` example shows `y: -8340` (negative Y value).
   - What's unclear: Whether game `+Y` is north (canvas top) or south (canvas bottom).
   - Recommendation: Default to standard cartographic orientation (+Y = up = canvas top). If markers appear mirrored, flip `cy = (WorldMaxY - worldY) / rangeY * canvasH` to `cy = (worldY - WorldMinY) / rangeY * canvasH`. Document the uncertainty.

---

## Localization Keys Required

### New Navigation Keys

```xml
<!-- Strings.en.axaml -->
<sys:String x:Key="Nav.Players">Players</sys:String>
<sys:String x:Key="Nav.Events">Events</sys:String>
<sys:String x:Key="Nav.SeaChart">Sea Chart</sys:String>
<sys:String x:Key="Nav.Editor">Config Editor</sys:String>

<!-- Strings.de.axaml -->
<sys:String x:Key="Nav.Players">Spieler</sys:String>
<sys:String x:Key="Nav.Events">Ereignisse</sys:String>
<sys:String x:Key="Nav.SeaChart">Seekarte</sys:String>
<sys:String x:Key="Nav.Editor">Konfiguration</sys:String>
```

### Players View Keys

```xml
<sys:String x:Key="Players.Title">Players</sys:String>
<sys:String x:Key="Players.Subtitle">Connected players — refreshes automatically</sys:String>
<sys:String x:Key="Players.Column.Name">Name</sys:String>
<sys:String x:Key="Players.Column.SteamId">Steam ID</sys:String>
<sys:String x:Key="Players.Column.Alive">Alive</sys:String>
<sys:String x:Key="Players.Column.Session">Session</sys:String>
<sys:String x:Key="Players.Kick">Kick</sys:String>
<sys:String x:Key="Players.Ban">Ban</sys:String>
<sys:String x:Key="Players.Broadcast.Label">Broadcast message</sys:String>
<sys:String x:Key="Players.Broadcast.Send">Send</sys:String>
<sys:String x:Key="Players.Kick.Title">Kick Player</sys:String>
<sys:String x:Key="Players.Kick.Message">Kick {0} from the server?</sys:String>
<sys:String x:Key="Players.Ban.Title">Ban Player</sys:String>
<sys:String x:Key="Players.Ban.Permanent">Permanent ban</sys:String>
<sys:String x:Key="Players.Ban.Timed">Timed ban (minutes)</sys:String>
<sys:String x:Key="Players.Kicked">Player {0} kicked.</sys:String>
<sys:String x:Key="Players.Banned">Player {0} banned.</sys:String>
<sys:String x:Key="Players.Empty">No players connected.</sys:String>
<sys:String x:Key="Players.Error">Could not load player list.</sys:String>
```

### Events View Keys

```xml
<sys:String x:Key="Events.Title">Event Log</sys:String>
<sys:String x:Key="Events.Column.Time">Time</sys:String>
<sys:String x:Key="Events.Column.Type">Type</sys:String>
<sys:String x:Key="Events.Column.Player">Player</sys:String>
<sys:String x:Key="Events.Column.SteamId">Steam ID</sys:String>
<sys:String x:Key="Events.Filter.Placeholder">Filter by name, Steam ID, or type...</sys:String>
<sys:String x:Key="Events.Empty">No events recorded yet.</sys:String>
<sys:String x:Key="Events.Type.Join">Join</sys:String>
<sys:String x:Key="Events.Type.Leave">Leave</sys:String>
```

### Sea Chart View Keys

```xml
<sys:String x:Key="SeaChart.Title">Sea Chart</sys:String>
<sys:String x:Key="SeaChart.GenerateMap">Generate Map</sys:String>
<sys:String x:Key="SeaChart.NoMap">No map generated yet. Click 'Generate Map' to create one.</sys:String>
<sys:String x:Key="SeaChart.Player.Alive">Alive</sys:String>
<sys:String x:Key="SeaChart.Player.Dead">Dead</sys:String>
```

### Editor View Keys

```xml
<sys:String x:Key="Editor.Title">Config Editor</sys:String>
<sys:String x:Key="Editor.Category.Server">Server</sys:String>
<sys:String x:Key="Editor.Category.Multipliers">Multipliers</sys:String>
<sys:String x:Key="Editor.Save">Save</sys:String>
<sys:String x:Key="Editor.Saved">Configuration saved.</sys:String>
<sys:String x:Key="Editor.RestartRequired">Server must be restarted for changes to take effect.</sys:String>
<sys:String x:Key="Editor.ValidationError">One or more values are invalid. Please correct them before saving.</sys:String>
```

---

## Validation Architecture

`workflow.nyquist_validation` is absent from `.planning/config.json` — treat as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| Quick run command | `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase11" -x` |
| Full suite command | `dotnet test tests/WindroseServerManager.Core.Tests -x` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PLAYER-01 | `WindrosePlusApiService.GetStatusAsync` parses `/api/status` JSON into player list | unit | `dotnet test --filter "FullyQualifiedName~Phase11" -x` | ❌ Wave 0 |
| PLAYER-01 | Returns null on HTTP failure without throwing | unit | same | ❌ Wave 0 |
| PLAYER-02 | `RconAsync` sends correct JSON body with password + kick command | unit | same | ❌ Wave 0 |
| PLAYER-03 | Ban command builds correct RCON string for timed + permanent variants | unit | same | ❌ Wave 0 |
| PLAYER-04 | Broadcast `wp.say {message}` escapes special characters | unit | same | ❌ Wave 0 |
| EVENT-01 | `TryParseLine` parses join/leave JSON correctly | unit | same | ❌ Wave 0 |
| EVENT-01 | `TryParseLine` returns null for malformed lines without throwing | unit | same | ❌ Wave 0 |
| EVENT-01 | `TryParseLine` returns null for unknown event types | unit | same | ❌ Wave 0 |
| EVENT-02 | Filter logic matches on name, steamId, type (case-insensitive) | unit | same | ❌ Wave 0 |
| CHART-01 | `WorldToCanvas` maps (0, 0) to canvas center | unit | same | ❌ Wave 0 |
| CHART-01 | `WorldToCanvas` maps min/max extent to canvas edges | unit | same | ❌ Wave 0 |
| EDITOR-01 | `WindrosePlusApiService.ReadConfig` deserializes `windrose_plus.json` correctly | unit | same | ❌ Wave 0 |
| EDITOR-02 | Schema validation rejects out-of-range float multiplier | unit | same | ❌ Wave 0 |
| EDITOR-03 | `WriteConfigAsync` writes to temp file then moves atomically | unit | same | ❌ Wave 0 |

> Note: VIEW-layer behavior (DataGrid virtualization, FileSystemWatcher wiring, Canvas rendering, Popup) is tested manually; Core service logic is fully unit-testable.

### Sampling Rate

- **Per task commit:** `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~Phase11" -x`
- **Per wave merge:** `dotnet test tests/WindroseServerManager.Core.Tests -x`
- **Phase gate:** Full suite green (currently 112 tests) before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/WindrosePlusApiServiceTests.cs` — covers PLAYER-01, PLAYER-02, PLAYER-03, PLAYER-04 (HTTP response parsing, RCON body construction)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/EventsLogParserTests.cs` — covers EVENT-01, EVENT-02 (line parsing, filter logic)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/SeaChartMathTests.cs` — covers CHART-01 (coordinate transform)
- [ ] `tests/WindroseServerManager.Core.Tests/Phase11/EditorConfigTests.cs` — covers EDITOR-01, EDITOR-02, EDITOR-03 (parse, validate, atomic write)
- [ ] `IWindrosePlusApiService.cs` — new interface in Core (must exist before test stubs compile)
- [ ] `WindrosePlusPlayer.cs`, `WindrosePlusQueryResult.cs`, `WindrosePlusEvent.cs`, `WindrosePlusConfig.cs` — new Core models

---

## Sources

### Primary (HIGH confidence)

- Codebase: `src/WindroseServerManager.App/ViewModels/MainWindowViewModel.cs` — NavItem + DI registration pattern
- Codebase: `src/WindroseServerManager.App/ViewModels/DashboardViewModel.cs` — timer polling + HttpClient pattern
- Codebase: `src/WindroseServerManager.App/Views/Dialogs/ConfirmDialog.axaml` — existing confirmation dialog
- Codebase: `src/WindroseServerManager.Core/Services/WindrosePlusService.cs` — atomic file write pattern, HttpClient usage
- Codebase: `src/WindroseServerManager.Core/Services/ServerEventLog.cs` — tail-read and append pattern
- Codebase: `src/WindroseServerManager.Core/Models/AppSettings.cs` — per-server WindrosePlus settings fields
- Memory: `project_windroseplus_maintainer_agreement.md` — confirms `/api/rcon` HTTP surface, `POST /api/rcon { password, command }` format, API stability guarantee

### Secondary (MEDIUM confidence)

- WebFetch: `docs/commands.md` on GitHub — confirmed `POST /api/rcon` endpoint + request/response format
- WebFetch: `docs/config-reference.md` — `windrose_plus.json` schema (Server + Multipliers sections)
- WebFetch: WindrosePlus README — `/query` response example with player positions `{ x, y }`
- WebFetch: `docs/scripting-guide.md` — confirms join/leave events via Lua hooks → infers events.log schema

### Tertiary (LOW confidence)

- events.log JSON field names (`steamId`, `name`, `type`, `timestamp`) — inferred from Lua API surface, not from explicit log format documentation
- World coordinate bounds (±30000) — estimated from `/query` example values, not from official Windrose world size documentation
- Map image path (`windrose_plus_data/map.png`) — inferred from `wp.mapgen` command existence, not confirmed

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries already in project, no new NuGet packages
- HTTP API surface (GET /status, POST /rcon): HIGH — maintainer confirmed, Phase 10 precedent
- RCON command syntax (kick/ban): MEDIUM — commands exist but exact syntax not in public docs
- events.log schema: LOW — no official schema; inferred from Lua scripting API
- Sea-chart world bounds: LOW — single data point from README example, no official extent
- Editor config schema (windrose_plus.json): HIGH — confirmed via docs + maintainer

**Research date:** 2026-04-20
**Valid until:** 2026-05-20 (WindrosePlus API declared stable by maintainer; additive changes only in point releases)
