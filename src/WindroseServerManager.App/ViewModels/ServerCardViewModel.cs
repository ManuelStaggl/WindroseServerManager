using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.ViewModels;

/// <summary>
/// Per-server view on the Server page. Holds presentational state + the per-server
/// auto-start flag (two-way-bound toggle). When the toggle changes we fire
/// <see cref="AutoStartChanged"/> so the parent view-model can persist to settings —
/// this keeps the card VM free of service dependencies.
/// </summary>
public sealed partial class ServerCardViewModel : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _autoStartOnAppLaunch;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _hasLiveMap;
    [ObservableProperty] private string? _liveMapUrl;
    [ObservableProperty] private bool _hasWindrosePlusUpdate;
    [ObservableProperty] private string? _installedWindrosePlusTag;
    [ObservableProperty] private string? _latestWindrosePlusTag;
    [ObservableProperty] private bool _isUpdatingWindrosePlus;

    public string Id { get; }
    public string Name { get; }
    public string InstallDir { get; }
    public bool WindrosePlusActive { get; }

    /// <summary>Fired whenever <see cref="AutoStartOnAppLaunch"/> changes from a UI binding.
    /// Not raised during initial construction.</summary>
    public event Action<string, bool>? AutoStartChanged;

    private bool _suppressAutoStartEvent;

    public ServerCardViewModel(
        string id,
        string name,
        string installDir,
        bool isActive,
        bool windrosePlusActive,
        bool autoStartOnAppLaunch,
        bool isRunning,
        string? liveMapUrl)
    {
        Id = id;
        Name = name;
        InstallDir = installDir;
        _isActive = isActive;
        WindrosePlusActive = windrosePlusActive;
        _suppressAutoStartEvent = true;
        _autoStartOnAppLaunch = autoStartOnAppLaunch;
        _suppressAutoStartEvent = false;
        _isRunning = isRunning;
        _liveMapUrl = liveMapUrl;
        _hasLiveMap = !string.IsNullOrWhiteSpace(liveMapUrl);
    }

    partial void OnAutoStartOnAppLaunchChanged(bool value)
    {
        if (_suppressAutoStartEvent) return;
        AutoStartChanged?.Invoke(Id, value);
    }

    partial void OnLiveMapUrlChanged(string? value) =>
        HasLiveMap = !string.IsNullOrWhiteSpace(value);
}
