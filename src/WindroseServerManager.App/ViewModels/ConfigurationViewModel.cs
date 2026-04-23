using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public enum CombatDifficultyOption
{
    Easy,
    Normal,
    Hard,
}

public partial class ConfigurationViewModel : ViewModelBase
{
    private readonly IServerConfigService _config;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;

    // Suppress the auto-custom preset switch while we populate from disk.
    private bool _suppressPresetAutoSwitch;

    [ObservableProperty] private ServerDescription? _server;
    [ObservableProperty] private WorldDescription? _world;
    [ObservableProperty] private string? _selectedWorldId;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _inviteCodeError;
    [ObservableProperty] private string? _worldNameError;

    // World parameter flat properties
    [ObservableProperty] private double _mobHealth = 1.0;
    [ObservableProperty] private double _mobDamage = 1.0;
    [ObservableProperty] private double _shipsHealth = 1.0;
    [ObservableProperty] private double _shipsDamage = 1.0;
    [ObservableProperty] private double _boardingDifficulty = 1.0;
    [ObservableProperty] private double _coopStatsCorrection = 1.0;
    [ObservableProperty] private double _coopShipStatsCorrection = 0.0;

    [ObservableProperty] private bool _coopSharedQuests = true;
    [ObservableProperty] private bool _easyExplore;

    [ObservableProperty] private CombatDifficultyOption _combatDifficulty = CombatDifficultyOption.Normal;

    [ObservableProperty] private WorldPresetType _worldPresetType = WorldPresetType.Medium;
    [ObservableProperty] private string _worldName = string.Empty;

    public ObservableCollection<string> WorldIds { get; } = new();

    public IReadOnlyList<WorldPresetType> PresetOptions { get; } = new[]
    {
        WorldPresetType.Easy, WorldPresetType.Medium, WorldPresetType.Hard, WorldPresetType.Custom,
    };

    public IReadOnlyList<CombatDifficultyOption> CombatOptions { get; } = new[]
    {
        CombatDifficultyOption.Easy, CombatDifficultyOption.Normal, CombatDifficultyOption.Hard,
    };

    public string? PersistentServerId => Server?.PersistentServerId is { Length: > 0 } s ? s : null;

    private string? _lastLoadedServerDir;

    public ConfigurationViewModel(IServerConfigService config, IAppSettingsService settings, IToastService toasts)
    {
        _config = config;
        _settings = settings;
        _toasts = toasts;
        _lastLoadedServerDir = _settings.ActiveServerDir;
        _settings.Changed += OnSettingsChanged;
        _ = ReloadAsync();
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        // Active server kann sich nach dem App-Start ändern (z. B. erster Install via Wizard).
        // Dann müssen wir die stale Default-Werte durch die realen ServerDescription-Daten ersetzen.
        var currentDir = _settings.ActiveServerDir;
        if (!string.Equals(currentDir, _lastLoadedServerDir, StringComparison.OrdinalIgnoreCase))
        {
            _lastLoadedServerDir = currentDir;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { _ = ReloadAsync(); });
        }
    }

    public bool CanOpenInstallDir
    {
        get
        {
            var d = _settings?.Current?.ServerInstallDir;
            return !string.IsNullOrWhiteSpace(d) && Directory.Exists(d);
        }
    }

    public bool CanOpenServerDescription
    {
        get
        {
            var p = _config?.GetServerDescriptionPath();
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p);
        }
    }

    public bool CanOpenWorldsRoot
    {
        get
        {
            var p = _config?.GetWorldsRoot();
            return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p);
        }
    }

    public bool CanOpenSelectedWorldDir
    {
        get
        {
            if (string.IsNullOrEmpty(SelectedWorldId)) return false;
            var p = _config?.GetWorldDir(SelectedWorldId!);
            return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p);
        }
    }

    [RelayCommand]
    private void OpenInstallDir()
    {
        var p = _settings.ActiveServerDir;
        if (!CanOpenInstallDir) return;
        try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenServerDescription()
    {
        var p = _config.GetServerDescriptionPath();
        if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) return;
        try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenWorldsRoot()
    {
        var p = _config.GetWorldsRoot();
        if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) return;
        try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenSelectedWorldDir()
    {
        if (string.IsNullOrEmpty(SelectedWorldId)) return;
        var p = _config.GetWorldDir(SelectedWorldId!);
        if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) return;
        try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
    }

    partial void OnServerChanged(ServerDescription? value)
    {
        OnPropertyChanged(nameof(PersistentServerId));
        ValidateInviteCode();
    }

    private void ValidateInviteCode()
    {
        InviteCodeError = ServerDescription.ValidateInviteCode(Server?.InviteCode);
    }

    // Hook into Server.InviteCode edits via a thin wrapper property bound from XAML.
    public string? InviteCode
    {
        get => Server?.InviteCode;
        set
        {
            if (Server is null) return;
            if (Server.InviteCode == value) return;
            Server.InviteCode = value ?? string.Empty;
            OnPropertyChanged();
            ValidateInviteCode();
        }
    }

    [RelayCommand]
    private async Task CopyIslandIdAsync()
    {
        if (string.IsNullOrEmpty(SelectedWorldId)) return;
        var top = GetOwnerWindow();
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(SelectedWorldId);
        _toasts.Success(Loc.Format("Toast.IslandIdCopiedFormat", SelectedWorldId));
    }

    [RelayCommand]
    private void GenerateInviteCode()
    {
        if (Server is null) return;
        InviteCode = Core.Services.InviteCodeGenerator.Generate();
        _toasts.Info(Loc.Format("Toast.NewInviteFormat", InviteCode));
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        ErrorMessage = null;
        try
        {
            Server = await _config.LoadServerDescriptionAsync() ?? new ServerDescription();
            // Notify wrapper properties
            OnPropertyChanged(nameof(InviteCode));
            WorldIds.Clear();
            foreach (var id in _config.ListWorldIds()) WorldIds.Add(id);
            SelectedWorldId = Server.WorldIslandId is { Length: > 0 } wid && WorldIds.Contains(wid)
                ? wid
                : WorldIds.FirstOrDefault();
            await LoadWorldAsync();
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    partial void OnSelectedWorldIdChanged(string? value)
    {
        _ = LoadWorldAsync();
        OnPropertyChanged(nameof(CanOpenSelectedWorldDir));
    }

    private async Task LoadWorldAsync()
    {
        if (string.IsNullOrEmpty(SelectedWorldId)) { World = null; return; }
        try
        {
            World = await _config.LoadWorldDescriptionAsync(SelectedWorldId);
            PopulateFromWorld();
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    private void PopulateFromWorld()
    {
        _suppressPresetAutoSwitch = true;
        try
        {
            if (World is null)
            {
                WorldName = string.Empty;
                WorldPresetType = WorldPresetType.Medium;
                return;
            }

            WorldName = World.WorldName;
            WorldPresetType = World.WorldPresetType;

            MobHealth = GetFloat(WorldParameterCatalog.MobHealth);
            MobDamage = GetFloat(WorldParameterCatalog.MobDamage);
            ShipsHealth = GetFloat(WorldParameterCatalog.ShipsHealth);
            ShipsDamage = GetFloat(WorldParameterCatalog.ShipsDamage);
            BoardingDifficulty = GetFloat(WorldParameterCatalog.BoardingDiff);
            CoopStatsCorrection = GetFloat(WorldParameterCatalog.CoopStatsCorr);
            CoopShipStatsCorrection = GetFloat(WorldParameterCatalog.CoopShipStatsCorr);

            CoopSharedQuests = GetBool(WorldParameterCatalog.CoopSharedQuests);
            EasyExplore = GetBool(WorldParameterCatalog.EasyExplore);

            CombatDifficulty = GetTag(WorldParameterCatalog.CombatDifficulty) switch
            {
                WorldParameterCatalog.CombatEasy   => CombatDifficultyOption.Easy,
                WorldParameterCatalog.CombatHard   => CombatDifficultyOption.Hard,
                _                                   => CombatDifficultyOption.Normal,
            };
        }
        finally
        {
            _suppressPresetAutoSwitch = false;
        }
    }

    private double GetFloat(string tagName)
    {
        var (_, _, def) = WorldParameterCatalog.GetRange(tagName);
        if (World?.WorldSettings.FloatParameters is { } d)
        {
            // Try both canonical key format and raw tag-name storage
            var k1 = WorldParameterCatalog.MakeKey(tagName);
            if (d.TryGetValue(k1, out var v)) return v;
            if (d.TryGetValue(tagName, out var v2)) return v2;
        }
        return def;
    }

    private bool GetBool(string tagName)
    {
        var def = WorldParameterCatalog.GetBoolDefault(tagName);
        if (World?.WorldSettings.BoolParameters is { } d)
        {
            var k1 = WorldParameterCatalog.MakeKey(tagName);
            if (d.TryGetValue(k1, out var v)) return v;
            if (d.TryGetValue(tagName, out var v2)) return v2;
        }
        return def;
    }

    private string? GetTag(string tagName)
    {
        if (World?.WorldSettings.TagParameters is { } d)
        {
            var k1 = WorldParameterCatalog.MakeKey(tagName);
            if (d.TryGetValue(k1, out var v)) return v?.TagName;
            if (d.TryGetValue(tagName, out var v2)) return v2?.TagName;
        }
        return null;
    }

    // Auto-switch to Custom when user tweaks any parameter
    partial void OnMobHealthChanged(double value) => MaybeSwitchToCustom();
    partial void OnMobDamageChanged(double value) => MaybeSwitchToCustom();
    partial void OnShipsHealthChanged(double value) => MaybeSwitchToCustom();
    partial void OnShipsDamageChanged(double value) => MaybeSwitchToCustom();
    partial void OnBoardingDifficultyChanged(double value) => MaybeSwitchToCustom();
    partial void OnCoopStatsCorrectionChanged(double value) => MaybeSwitchToCustom();
    partial void OnCoopShipStatsCorrectionChanged(double value) => MaybeSwitchToCustom();
    partial void OnCoopSharedQuestsChanged(bool value) => MaybeSwitchToCustom();
    partial void OnEasyExploreChanged(bool value) => MaybeSwitchToCustom();
    partial void OnCombatDifficultyChanged(CombatDifficultyOption value) => MaybeSwitchToCustom();

    partial void OnWorldPresetTypeChanged(WorldPresetType value)
    {
        if (_suppressPresetAutoSwitch) return;
        if (value == WorldPresetType.Custom) return;
        ApplyPresetDefaults(value);
    }

    /// <summary>
    /// Setzt alle Parameter auf die Preset-Defaults. Läuft mit Suppress-Flag
    /// damit die individuellen OnXxxChanged nicht sofort wieder auf Custom zurückspringen.
    /// </summary>
    private void ApplyPresetDefaults(WorldPresetType preset)
    {
        _suppressPresetAutoSwitch = true;
        try
        {
            switch (preset)
            {
                case WorldPresetType.Easy:
                    MobHealth = 0.7; MobDamage = 0.7;
                    ShipsHealth = 0.8; ShipsDamage = 0.7;
                    BoardingDifficulty = 0.7;
                    CoopStatsCorrection = 0.5; CoopShipStatsCorrection = 0.0;
                    CombatDifficulty = CombatDifficultyOption.Easy;
                    CoopSharedQuests = true; EasyExplore = false;
                    break;
                case WorldPresetType.Medium:
                    MobHealth = 1.0; MobDamage = 1.0;
                    ShipsHealth = 1.0; ShipsDamage = 1.0;
                    BoardingDifficulty = 1.0;
                    CoopStatsCorrection = 1.0; CoopShipStatsCorrection = 0.0;
                    CombatDifficulty = CombatDifficultyOption.Normal;
                    CoopSharedQuests = true; EasyExplore = false;
                    break;
                case WorldPresetType.Hard:
                    MobHealth = 1.3; MobDamage = 1.3;
                    ShipsHealth = 1.3; ShipsDamage = 1.3;
                    BoardingDifficulty = 1.5;
                    CoopStatsCorrection = 1.5; CoopShipStatsCorrection = 0.5;
                    CombatDifficulty = CombatDifficultyOption.Hard;
                    CoopSharedQuests = true; EasyExplore = true;
                    break;
            }
            _toasts.Info(Loc.Format("Configuration.PresetApplied", Loc.Get($"WorldPreset.{preset}")));
        }
        finally
        {
            _suppressPresetAutoSwitch = false;
        }
    }

    partial void OnWorldNameChanged(string value)
    {
        WorldNameError = string.IsNullOrWhiteSpace(value) ? Loc.Get("Configuration.WorldNameEmpty") : null;
    }

    private void MaybeSwitchToCustom()
    {
        if (_suppressPresetAutoSwitch) return;
        if (WorldPresetType != WorldPresetType.Custom)
        {
            WorldPresetType = WorldPresetType.Custom;
        }
    }

    [RelayCommand]
    private async Task SaveServerAsync()
    {
        if (Server is null) return;
        ValidateInviteCode();
        if (!string.IsNullOrEmpty(InviteCodeError))
        {
            _toasts.Error(InviteCodeError);
            return;
        }
        try
        {
            await _config.SaveServerDescriptionAsync(Server);

            // Name in der Server-Liste (ServerEntry) mit dem in-game Namen synchron halten,
            // damit Dashboard / Server-Page / Configuration immer denselben Namen anzeigen.
            var activeDir = _settings.ActiveServerDir;
            var newName = Server.ServerName;
            if (!string.IsNullOrWhiteSpace(activeDir) && !string.IsNullOrWhiteSpace(newName))
            {
                await _settings.UpdateAsync(s =>
                {
                    var entry = s.Servers.FirstOrDefault(e =>
                        string.Equals(e.InstallDir?.TrimEnd('\\', '/'), activeDir.TrimEnd('\\', '/'),
                            StringComparison.OrdinalIgnoreCase));
                    if (entry is not null && !string.Equals(entry.Name, newName, StringComparison.Ordinal))
                        entry.Name = newName;
                });
            }

            StatusMessage = Loc.Get("Toast.ServerDescriptionSaved");
            _toasts.Success(Loc.Get("Toast.ServerDescriptionSaved"));
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    private void WriteFloat(string tagName, double value)
    {
        if (World is null) return;
        var d = World.WorldSettings.FloatParameters;
        // Keep legacy raw-name key in sync if present, remove it to prefer canonical.
        d.Remove(tagName);
        d[WorldParameterCatalog.MakeKey(tagName)] = value;
    }

    private void WriteBool(string tagName, bool value)
    {
        if (World is null) return;
        var d = World.WorldSettings.BoolParameters;
        d.Remove(tagName);
        d[WorldParameterCatalog.MakeKey(tagName)] = value;
    }

    private void WriteTag(string tagName, string value)
    {
        if (World is null) return;
        var d = World.WorldSettings.TagParameters;
        d.Remove(tagName);
        d[WorldParameterCatalog.MakeKey(tagName)] = new TagValue(value);
    }

    [RelayCommand]
    private async Task SaveWorldAsync()
    {
        if (World is null || string.IsNullOrEmpty(SelectedWorldId)) return;
        if (string.IsNullOrWhiteSpace(WorldName))
        {
            WorldNameError = Loc.Get("Configuration.WorldNameEmpty");
            _toasts.Error(WorldNameError);
            return;
        }
        try
        {
            World.WorldName = WorldName;
            World.WorldPresetType = WorldPresetType;

            WriteFloat(WorldParameterCatalog.MobHealth, MobHealth);
            WriteFloat(WorldParameterCatalog.MobDamage, MobDamage);
            WriteFloat(WorldParameterCatalog.ShipsHealth, ShipsHealth);
            WriteFloat(WorldParameterCatalog.ShipsDamage, ShipsDamage);
            WriteFloat(WorldParameterCatalog.BoardingDiff, BoardingDifficulty);
            WriteFloat(WorldParameterCatalog.CoopStatsCorr, CoopStatsCorrection);
            WriteFloat(WorldParameterCatalog.CoopShipStatsCorr, CoopShipStatsCorrection);

            WriteBool(WorldParameterCatalog.CoopSharedQuests, CoopSharedQuests);
            WriteBool(WorldParameterCatalog.EasyExplore, EasyExplore);

            var combatValue = CombatDifficulty switch
            {
                CombatDifficultyOption.Easy => WorldParameterCatalog.CombatEasy,
                CombatDifficultyOption.Hard => WorldParameterCatalog.CombatHard,
                _                           => WorldParameterCatalog.CombatNormal,
            };
            WriteTag(WorldParameterCatalog.CombatDifficulty, combatValue);

            await _config.SaveWorldDescriptionAsync(SelectedWorldId, World);
            StatusMessage = Loc.Get("Toast.WorldDescriptionSaved");
            _toasts.Success(Loc.Get("Toast.WorldDescriptionSaved"));
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task CreateWorldAsync()
    {
        try
        {
            var id = Guid.NewGuid().ToString("N").ToUpperInvariant();
            var newWorld = new WorldDescription
            {
                IslandId = id,
                WorldName = Loc.Format("Configuration.NewWorldName", DateTime.Now),
                WorldPresetType = WorldPresetType.Medium,
                CreationTime = (double)DateTime.UtcNow.Ticks,
            };

            // Prime with catalogue defaults so the file is complete
            foreach (var key in WorldParameterCatalog.KnownFloatKeys)
            {
                var (_, _, def) = WorldParameterCatalog.GetRange(key);
                newWorld.WorldSettings.FloatParameters[WorldParameterCatalog.MakeKey(key)] = def;
            }
            foreach (var key in WorldParameterCatalog.KnownBoolKeys)
            {
                newWorld.WorldSettings.BoolParameters[WorldParameterCatalog.MakeKey(key)] =
                    WorldParameterCatalog.GetBoolDefault(key);
            }
            newWorld.WorldSettings.TagParameters[WorldParameterCatalog.MakeKey(WorldParameterCatalog.CombatDifficulty)] =
                new TagValue(WorldParameterCatalog.CombatNormal);

            await _config.SaveWorldDescriptionAsync(id, newWorld);
            WorldIds.Add(id);
            SelectedWorldId = id;
            _toasts.Success(Loc.Format("Toast.WorldCreatedFormat", newWorld.WorldName));
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task ActivateWorldAsync()
    {
        if (Server is null || string.IsNullOrEmpty(SelectedWorldId)) return;
        try
        {
            Server.WorldIslandId = SelectedWorldId!;
            await _config.SaveServerDescriptionAsync(Server);
            var m = Loc.Format("Toast.ActiveWorldFormat", SelectedWorldId);
            StatusMessage = m;
            _toasts.Success(m);
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task DeleteWorldAsync()
    {
        if (string.IsNullOrEmpty(SelectedWorldId)) return;
        var id = SelectedWorldId!;

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                Loc.Get("Confirm.WorldDelete.Title"),
                Loc.Format("Confirm.WorldDelete.MessageFormat", id),
                confirmLabel: Loc.Get("Confirm.WorldDelete.Label"),
                danger: true);
            if (!confirmed) return;
        }

        try
        {
            await _config.DeleteWorldAsync(id);
            WorldIds.Remove(id);
            SelectedWorldId = WorldIds.FirstOrDefault();
            _toasts.Success(Loc.Format("Toast.WorldDeletedFormat", id));
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    private static Window? GetOwnerWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow
            : null;
    }
}
