using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class PlayersViewModel : ViewModelBase, IDisposable
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private readonly System.Timers.Timer _pollTimer;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<WindrosePlusPlayer> _players = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private WindrosePlusPlayer? _selectedPlayer;
    [ObservableProperty] private string _broadcastMessage = string.Empty;

    public bool HasPlayers => Players.Count > 0;
    public bool HasNoPlayers => !IsLoading && string.IsNullOrEmpty(ErrorMessage) && Players.Count == 0;

    public PlayersViewModel(IWindrosePlusApiService api, IAppSettingsService settings, IToastService toasts)
    {
        _api = api;
        _settings = settings;
        _toasts = toasts;
        _pollTimer = new System.Timers.Timer();
        _pollTimer.Elapsed += async (_, _) => await RefreshAsync().ConfigureAwait(false);
        _pollTimer.AutoReset = true;
        Players.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPlayers));
            OnPropertyChanged(nameof(HasNoPlayers));
        };
    }

    public void Start()
    {
        var interval = Math.Max(3, _settings.Current.WindrosePlusPlayerRefreshSeconds);
        _pollTimer.Interval = interval * 1000;
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public void Stop()
    {
        _pollTimer.Stop();
        _cts?.Cancel();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { IsLoading = true; ErrorMessage = null; });
            var result = await _api.GetStatusAsync(serverDir, ct).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result is null)
                {
                    ErrorMessage = Loc.Get("Players.Error");
                    Players.Clear();
                }
                else
                {
                    DiffUpdate(result.Players);
                }
            });
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (Exception ex)
        {
            Log.Warning(ex, "Players poll failed");
            await Dispatcher.UIThread.InvokeAsync(() => { ErrorMessage = Loc.Get("Players.Error"); });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoPlayers));
            });
        }
    }

    private void DiffUpdate(System.Collections.Generic.IReadOnlyList<WindrosePlusPlayer> next)
    {
        for (int i = Players.Count - 1; i >= 0; i--)
            if (!next.Any(p => p.SteamId == Players[i].SteamId)) Players.RemoveAt(i);
        foreach (var p in next)
        {
            var idx = -1;
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].SteamId == p.SteamId) { idx = i; break; }
            if (idx >= 0) Players[idx] = p;
            else Players.Add(p);
        }
    }

    [RelayCommand]
    private async Task KickAsync(WindrosePlusPlayer? player)
    {
        if (player is null) return;
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;

        var window = GetMainWindow();
        if (window is null) return;

        var ok = await ConfirmDialog.ShowAsync(window,
            Loc.Get("Players.Kick.Title"),
            Loc.Format("Players.Kick.Message", player.Name),
            Loc.Get("Players.Kick"),
            danger: true);
        if (!ok) return;

        var resp = await _api.RconAsync(serverDir, _api.BuildKickCommand(player.SteamId));
        if (resp is null) _toasts.Error(Loc.Get("Players.Error"));
        else _toasts.Success(Loc.Format("Players.Kicked", player.Name));
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task BanAsync(WindrosePlusPlayer? player)
    {
        if (player is null) return;
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;

        var window = GetMainWindow();
        if (window is null) return;

        // PLAYER-03: BanDialog bietet Permanent vs. Befristet (Minuten-Eingabe).
        var result = await BanDialog.ShowAsync(window,
            Loc.Get("Players.Ban.Title"),
            Loc.Format("Players.Ban.Message", player.Name));
        if (result is null) return; // abgebrochen

        var resp = await _api.RconAsync(serverDir, _api.BuildBanCommand(player.SteamId, result.Minutes));
        if (resp is null)
        {
            _toasts.Error(Loc.Get("Players.Error"));
        }
        else
        {
            var msg = result.Minutes is null
                ? Loc.Format("Players.Banned", player.Name)
                : Loc.Format("Players.BannedTimed", player.Name, result.Minutes.Value);
            _toasts.Success(msg);
        }
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task BroadcastAsync()
    {
        var msg = BroadcastMessage?.Trim();
        if (string.IsNullOrWhiteSpace(msg)) return;
        var serverDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;

        var resp = await _api.RconAsync(serverDir, _api.BuildBroadcastCommand(msg));
        if (resp is null) _toasts.Error(Loc.Get("Players.Error"));
        else
        {
            _toasts.Success(Loc.Get("Players.Broadcast.Sent"));
            BroadcastMessage = string.Empty;
        }
    }

    private static Avalonia.Controls.Window? GetMainWindow() =>
        Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
        ? desktop.MainWindow : null;

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
