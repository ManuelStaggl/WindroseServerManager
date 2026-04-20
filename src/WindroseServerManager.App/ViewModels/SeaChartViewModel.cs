using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class SeaChartViewModel : ViewModelBase, IDisposable
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private readonly System.Timers.Timer _pollTimer;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<PlayerMarkerViewModel> _markers = new();
    [ObservableProperty] private PlayerMarkerViewModel? _selectedMarker;
    [ObservableProperty] private Bitmap? _mapImage;
    [ObservableProperty] private bool _hasMap;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private double _canvasWidth  = 800;
    [ObservableProperty] private double _canvasHeight = 800;

    // Auto-expanding world bounds (research: LOW confidence on extent, expand as data arrives)
    private double _worldMinX = -30000, _worldMaxX = 30000;
    private double _worldMinY = -30000, _worldMaxY = 30000;

    public SeaChartViewModel(IWindrosePlusApiService api, IAppSettingsService settings)
    {
        _api = api;
        _settings = settings;
        _pollTimer = new System.Timers.Timer(5000) { AutoReset = true };
        _pollTimer.Elapsed += async (_, _) => await RefreshAsync().ConfigureAwait(false);
    }

    public void Start()
    {
        TryLoadMap();
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public void Stop()
    {
        _pollTimer.Stop();
        _cts?.Cancel();
    }

    public void UpdateCanvasSize(double w, double h)
    {
        CanvasWidth = w;
        CanvasHeight = h;
        RecomputeMarkerPositions();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _api.QueryAsync(serverDir, _cts.Token).ConfigureAwait(false);
            if (result is null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ErrorMessage = Loc.Get("SeaChart.Error"));
                return;
            }
            ExpandBounds(result.Players);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = null;
                DiffMarkers(result.Players);
                RecomputeMarkerPositions();
            });
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            Log.Warning(ex, "SeaChart /query failed");
            await Dispatcher.UIThread.InvokeAsync(() => ErrorMessage = Loc.Get("SeaChart.Error"));
        }
    }

    private void ExpandBounds(IReadOnlyList<WindrosePlusPlayer> players)
    {
        foreach (var p in players)
        {
            if (!p.WorldX.HasValue || !p.WorldY.HasValue) continue;
            if (p.WorldX.Value < _worldMinX) _worldMinX = p.WorldX.Value;
            if (p.WorldX.Value > _worldMaxX) _worldMaxX = p.WorldX.Value;
            if (p.WorldY.Value < _worldMinY) _worldMinY = p.WorldY.Value;
            if (p.WorldY.Value > _worldMaxY) _worldMaxY = p.WorldY.Value;
        }
    }

    private void DiffMarkers(IReadOnlyList<WindrosePlusPlayer> next)
    {
        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            var still = false;
            foreach (var n in next) if (n.SteamId == Markers[i].Player.SteamId) { still = true; break; }
            if (!still) Markers.RemoveAt(i);
        }
        foreach (var p in next)
        {
            PlayerMarkerViewModel? existing = null;
            foreach (var m in Markers) if (m.Player.SteamId == p.SteamId) { existing = m; break; }
            if (existing is not null) existing.Player = p;
            else Markers.Add(new PlayerMarkerViewModel(p, OnMarkerSelected));
        }
    }

    private void RecomputeMarkerPositions()
    {
        foreach (var m in Markers)
        {
            if (!m.Player.WorldX.HasValue || !m.Player.WorldY.HasValue) continue;
            var (cx, cy) = SeaChartMath.WorldToCanvas(
                m.Player.WorldX.Value, m.Player.WorldY.Value,
                CanvasWidth, CanvasHeight,
                _worldMinX, _worldMaxX, _worldMinY, _worldMaxY);
            m.CanvasX = cx - 6;  // center 12px ellipse
            m.CanvasY = cy - 6;
        }
    }

    private void OnMarkerSelected(PlayerMarkerViewModel marker)
    {
        SelectedMarker = ReferenceEquals(SelectedMarker, marker) ? null : marker;
    }

    [RelayCommand]
    private void ClearSelection() => SelectedMarker = null;

    [RelayCommand]
    private async Task GenerateMapAsync()
    {
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;
        await _api.RconAsync(serverDir, "wp.mapgen");
        // Map generation is async server-side; poll for file for 30s
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1000);
            if (TryLoadMap()) return;
        }
    }

    private bool TryLoadMap()
    {
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return false;
        var path = Path.Combine(serverDir, "windrose_plus_data", "map.png");
        if (!File.Exists(path)) { HasMap = false; return false; }
        try
        {
            MapImage = new Bitmap(path);
            HasMap = true;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load map image {Path}", path);
            HasMap = false;
            return false;
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        MapImage?.Dispose();
    }
}
