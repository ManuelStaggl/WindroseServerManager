using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public enum LogLevelFilter
{
    All,
    InfoPlus,
    WarningPlus,
    ErrorOnly
}

public partial class ServerControlViewModel : ViewModelBase, IDisposable
{
    private readonly IServerProcessService _proc;
    private readonly IAppSettingsService _settings;
    private readonly IServerConfigService _config;
    private readonly IToastService _toasts;
    private readonly System.Timers.Timer _refreshTimer;

    [ObservableProperty] private ServerStatus _status;
    [ObservableProperty] private string _uptimeText = "—";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _scheduledRestartEnabled;
    [ObservableProperty] private string _dailyRestartTime = "04:00";
    [ObservableProperty] private string? _inviteCode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private LogLevelFilter _currentLogFilter = LogLevelFilter.All;

    public bool CanOpenServerDir => !string.IsNullOrWhiteSpace(_settings.Current.ServerInstallDir)
                                    && Directory.Exists(_settings.Current.ServerInstallDir);

    public bool CanOpenServerDescription
    {
        get
        {
            var p = _config.GetServerDescriptionPath();
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p);
        }
    }

    public TimeSpan DailyRestartTimeSpan
    {
        get
        {
            if (TimeSpan.TryParseExact(DailyRestartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var ts))
                return ts;
            if (TimeSpan.TryParse(DailyRestartTime, CultureInfo.InvariantCulture, out ts))
                return ts;
            return TimeSpan.FromHours(4);
        }
        set
        {
            DailyRestartTime = value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
    }

    public ObservableCollection<string> Log { get; } = new();
    public ObservableCollection<string> FilteredLog { get; } = new();

    public bool IsAllFilter
    {
        get => CurrentLogFilter == LogLevelFilter.All;
        set { if (value) CurrentLogFilter = LogLevelFilter.All; }
    }
    public bool IsInfoPlusFilter
    {
        get => CurrentLogFilter == LogLevelFilter.InfoPlus;
        set { if (value) CurrentLogFilter = LogLevelFilter.InfoPlus; }
    }
    public bool IsWarningPlusFilter
    {
        get => CurrentLogFilter == LogLevelFilter.WarningPlus;
        set { if (value) CurrentLogFilter = LogLevelFilter.WarningPlus; }
    }
    public bool IsErrorOnlyFilter
    {
        get => CurrentLogFilter == LogLevelFilter.ErrorOnly;
        set { if (value) CurrentLogFilter = LogLevelFilter.ErrorOnly; }
    }

    public ServerControlViewModel(IServerProcessService proc, IAppSettingsService settings, IServerConfigService config, IToastService toasts)
    {
        _proc = proc;
        _settings = settings;
        _config = config;
        _toasts = toasts;
        _proc.StatusChanged += OnStatus;
        _proc.LogAppended += OnLog;
        _status = _proc.Status;

        foreach (var line in _proc.RecentLog) Log.Add(line.Text);
        RebuildFilteredLog();

        ScheduledRestartEnabled = settings.Current.ScheduledRestartEnabled;
        DailyRestartTime = settings.Current.DailyRestartTime;

        _ = LoadInviteCodeAsync();

        _refreshTimer = new System.Timers.Timer(5000);
        _refreshTimer.Elapsed += async (_, _) => await LoadInviteCodeAsync();
        _refreshTimer.Start();
    }

    partial void OnDailyRestartTimeChanged(string value)
    {
        OnPropertyChanged(nameof(DailyRestartTimeSpan));
    }

    partial void OnCurrentLogFilterChanged(LogLevelFilter value)
    {
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsInfoPlusFilter));
        OnPropertyChanged(nameof(IsWarningPlusFilter));
        OnPropertyChanged(nameof(IsErrorOnlyFilter));
        RebuildFilteredLog();
    }

    private static LogLevelFilter ClassifyLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return LogLevelFilter.InfoPlus;
        if (line.Contains("[FEHLER]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("!!!", StringComparison.Ordinal)
            || line.Contains("Error!", StringComparison.Ordinal)
            || System.Text.RegularExpressions.Regex.IsMatch(line, @"Log\w+:\s*Error:", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
            return LogLevelFilter.ErrorOnly;
        if (line.Contains("Warning:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[Warn]", StringComparison.OrdinalIgnoreCase))
            return LogLevelFilter.WarningPlus;
        return LogLevelFilter.InfoPlus;
    }

    private bool MatchesFilter(string line)
    {
        var level = ClassifyLine(line);
        return CurrentLogFilter switch
        {
            LogLevelFilter.All => true,
            LogLevelFilter.InfoPlus => level == LogLevelFilter.InfoPlus || level == LogLevelFilter.WarningPlus || level == LogLevelFilter.ErrorOnly,
            LogLevelFilter.WarningPlus => level == LogLevelFilter.WarningPlus || level == LogLevelFilter.ErrorOnly,
            LogLevelFilter.ErrorOnly => level == LogLevelFilter.ErrorOnly,
            _ => true,
        };
    }

    private void RebuildFilteredLog()
    {
        FilteredLog.Clear();
        foreach (var line in Log)
            if (MatchesFilter(line))
                FilteredLog.Add(line);
    }

    private async Task LoadInviteCodeAsync()
    {
        try
        {
            var desc = await _config.LoadServerDescriptionAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                InviteCode = string.IsNullOrWhiteSpace(desc?.InviteCode) ? null : desc!.InviteCode;
                OnPropertyChanged(nameof(CanOpenServerDir));
                OnPropertyChanged(nameof(CanOpenServerDescription));
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task CopyInviteCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteCode)) return;
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(InviteCode);
        _toasts.Success($"Invite-Code kopiert: {InviteCode}");
    }

    [RelayCommand]
    private void OpenServerDir()
    {
        var path = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenServerDescription()
    {
        var path = _config.GetServerDescriptionPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
    }

    private void OnStatus(ServerStatus s) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        Status = s;
        UpdateUptime();
    });

    private void OnLog(ServerLogLine line) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        Log.Add(line.Text);
        if (Log.Count > 500) Log.RemoveAt(0);

        if (MatchesFilter(line.Text))
        {
            FilteredLog.Add(line.Text);
            if (FilteredLog.Count > 500) FilteredLog.RemoveAt(0);
        }
    });

    private void UpdateUptime()
    {
        if (_proc.StartedAtUtc is null) { UptimeText = "—"; return; }
        var t = DateTime.UtcNow - _proc.StartedAtUtc.Value;
        UptimeText = t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";
    }

    private static Avalonia.Controls.Window? GetOwnerWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d
                ? d.MainWindow
                : null;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        ErrorMessage = _proc.ValidateCanStart();
        if (ErrorMessage is not null) { _toasts.Warning(ErrorMessage); return; }
        try { await _proc.StartAsync(); _toasts.Success("Server wird gestartet..."); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try { await _proc.StopAsync(); _toasts.Info("Server wird gestoppt..."); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task KillAsync()
    {
        // Kein Confirm-Dialog wenn der Server bereits (fast) aus ist.
        if (_proc.Status is ServerStatus.Running or ServerStatus.Starting)
        {
            var owner = GetOwnerWindow();
            if (owner is not null)
            {
                var confirmed = await ConfirmDialog.ShowAsync(
                    owner,
                    "Prozess hart beenden",
                    "Ungesicherte Welt-Änderungen gehen verloren. Trotzdem beenden?",
                    confirmLabel: "Hart beenden",
                    danger: true);
                if (!confirmed) return;
            }
        }

        try { await _proc.KillAsync(); _toasts.Warning("Server beendet (Force-Kill)."); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _toasts.Info("Neustart läuft...");

            try { await _proc.StopAsync(); }
            catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); return; }

            // Polling: max 10s, alle 500ms
            var maxWait = TimeSpan.FromSeconds(10);
            var step = TimeSpan.FromMilliseconds(500);
            var waited = TimeSpan.Zero;
            while (_proc.Status != ServerStatus.Stopped && waited < maxWait)
            {
                await Task.Delay(step);
                waited += step;
            }

            if (_proc.Status != ServerStatus.Stopped)
            {
                _toasts.Warning("Server konnte nicht rechtzeitig gestoppt werden.");
                return;
            }

            ErrorMessage = _proc.ValidateCanStart();
            if (ErrorMessage is not null) { _toasts.Warning(ErrorMessage); return; }

            try { await _proc.StartAsync(); _toasts.Success("Server wird neu gestartet..."); }
            catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveRestartScheduleAsync()
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                DailyRestartTime ?? string.Empty,
                @"^\d{2}:\d{2}$",
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(1)))
        {
            _toasts.Warning("Ungültiges Zeitformat (HH:mm erwartet).");
            return;
        }

        await _settings.UpdateAsync(s =>
        {
            s.ScheduledRestartEnabled = ScheduledRestartEnabled;
            s.DailyRestartTime = DailyRestartTime;
        });
        _toasts.Success("Restart-Zeitplan gespeichert.");
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _proc.StatusChanged -= OnStatus;
        _proc.LogAppended -= OnLog;
    }
}
