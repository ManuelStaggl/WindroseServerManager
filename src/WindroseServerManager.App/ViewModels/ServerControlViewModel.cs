using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
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
    private readonly IServerEventLog _eventLog;
    private readonly System.Timers.Timer _refreshTimer;

    public ObservableCollection<ServerEvent> Events { get; } = new();

    [ObservableProperty] private ServerStatus _status;
    [ObservableProperty] private string _uptimeText = "—";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _scheduledRestartEnabled;
    [ObservableProperty] private string _dailyRestartTime = "04:00";
    [ObservableProperty] private int _restartWarnMinutes = 5;
    [ObservableProperty] private bool _restartMon, _restartTue, _restartWed, _restartThu, _restartFri, _restartSat, _restartSun;

    [ObservableProperty] private bool _autoRestartOnHighRamEnabled;
    [ObservableProperty] private int _autoRestartRamThresholdPercent = 80;
    [ObservableProperty] private bool _autoRestartOnMaxUptimeEnabled;
    [ObservableProperty] private int _autoRestartMaxUptimeHours = 24;
    [ObservableProperty] private string? _inviteCode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private LogLevelFilter _currentLogFilter = LogLevelFilter.All;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private int _logBufferSize = 2000;

    public int[] LogBufferSizeOptions { get; } = { 500, 2000, 10000 };

    public string FilteredLinesDisplay => Loc.Format("ServerControl.LinesFormat", FilteredLog.Count);

    public bool CanOpenServerDir => !string.IsNullOrWhiteSpace(_settings.ActiveServerDir)
                                    && Directory.Exists(_settings.ActiveServerDir);

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

    public ServerControlViewModel(IServerProcessService proc, IAppSettingsService settings, IServerConfigService config, IToastService toasts, IServerEventLog eventLog, ILocalizationService localization)
    {
        FilteredLog.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FilteredLinesDisplay));
        localization.LanguageChanged += () => OnPropertyChanged(nameof(FilteredLinesDisplay));

        _proc = proc;
        _settings = settings;
        _config = config;
        _toasts = toasts;
        _eventLog = eventLog;
        _proc.StatusChanged += OnStatus;
        _proc.LogAppended += OnLog;
        _eventLog.Appended += OnEventAppended;
        _status = _proc.Status;

        foreach (var line in _proc.RecentLog) Log.Add(line.Text);
        RebuildFilteredLog();

        _ = LoadEventsAsync();

        ScheduledRestartEnabled = settings.Current.ScheduledRestartEnabled;
        DailyRestartTime = settings.Current.DailyRestartTime;
        RestartWarnMinutes = settings.Current.RestartWarnMinutes;
        LogBufferSize = settings.Current.LogBufferSize > 0 ? settings.Current.LogBufferSize : 2000;

        var days = settings.Current.RestartDays ?? new List<DayOfWeek>();
        // Leere Liste = täglich → alle Tage aktiv.
        var allDays = days.Count == 0;
        RestartMon = allDays || days.Contains(DayOfWeek.Monday);
        RestartTue = allDays || days.Contains(DayOfWeek.Tuesday);
        RestartWed = allDays || days.Contains(DayOfWeek.Wednesday);
        RestartThu = allDays || days.Contains(DayOfWeek.Thursday);
        RestartFri = allDays || days.Contains(DayOfWeek.Friday);
        RestartSat = allDays || days.Contains(DayOfWeek.Saturday);
        RestartSun = allDays || days.Contains(DayOfWeek.Sunday);

        AutoRestartOnHighRamEnabled = settings.Current.AutoRestartOnHighRamEnabled;
        AutoRestartRamThresholdPercent = settings.Current.AutoRestartRamThresholdPercent;
        AutoRestartOnMaxUptimeEnabled = settings.Current.AutoRestartOnMaxUptimeEnabled;
        AutoRestartMaxUptimeHours = settings.Current.AutoRestartMaxUptimeHours;

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

    partial void OnSearchQueryChanged(string value) => RebuildFilteredLog();

    partial void OnLogBufferSizeChanged(int value)
    {
        if (value <= 0) return;
        _ = _settings.UpdateAsync(s => s.LogBufferSize = value);
        TrimLog();
    }

    private void TrimLog()
    {
        var max = Math.Max(100, LogBufferSize);
        while (Log.Count > max) Log.RemoveAt(0);
        while (FilteredLog.Count > max) FilteredLog.RemoveAt(0);
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
        if (!string.IsNullOrWhiteSpace(SearchQuery)
            && line.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

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
        _toasts.Success(Loc.Format("Toast.InviteCopiedFormat", InviteCode));
    }

    [RelayCommand]
    private void OpenServerDir()
    {
        var path = _settings.ActiveServerDir;
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
        var max = Math.Max(100, LogBufferSize);
        Log.Add(line.Text);
        if (Log.Count > max) Log.RemoveAt(0);

        if (MatchesFilter(line.Text))
        {
            FilteredLog.Add(line.Text);
            if (FilteredLog.Count > max) FilteredLog.RemoveAt(0);
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
        try { await _proc.StartAsync(); _toasts.Success(Loc.Get("Toast.ServerStarting")); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try { await _proc.StopAsync(); _toasts.Info(Loc.Get("Toast.ServerStopping")); }
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
                    Loc.Get("Confirm.Kill.Title"),
                    Loc.Get("Confirm.Kill.Message"),
                    confirmLabel: Loc.Get("Confirm.Kill.Label"),
                    danger: true);
                if (!confirmed) return;
            }
        }

        try { await _proc.KillAsync(); _toasts.Warning(Loc.Get("Toast.ServerKilled")); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _toasts.Info(Loc.Get("Toast.RestartInProgress"));

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
                _toasts.Warning(Loc.Get("Toast.StopTooSlow"));
                return;
            }

            ErrorMessage = _proc.ValidateCanStart();
            if (ErrorMessage is not null) { _toasts.Warning(ErrorMessage); return; }

            try { await _proc.StartAsync(); _toasts.Success(Loc.Get("Toast.ServerRestarting")); }
            catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearSessionHistoryAsync()
    {
        await _eventLog.ClearAsync().ConfigureAwait(false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Events.Clear());
        _toasts.Info(Loc.Get("Toast.SessionHistoryCleared"));
    }

    [RelayCommand]
    private void ClearLog()
    {
        Log.Clear();
        FilteredLog.Clear();
        _toasts.Info(Loc.Get("Toast.LogCleared"));
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;

        var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.Get("ServerControl.Save.Title"),
            SuggestedFileName = $"windrose-log-{ts}.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(Loc.Get("ServerControl.Save.TextFile")) { Patterns = new[] { "*.txt" } },
            },
        });
        if (file is null) return;

        try
        {
            var path = file.Path.LocalPath;
            // Snapshot nehmen — Log kann währenddessen wachsen.
            var snapshot = Log.ToArray();
            await File.WriteAllLinesAsync(path, snapshot);
            _toasts.Success(Loc.Format("Toast.LogExportedFormat", Path.GetFileName(path)));
        }
        catch (Exception ex)
        {
            _toasts.Error(Loc.Format("Toast.ExportFailedFormat", ErrorMessageHelper.FriendlyMessage(ex)));
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var installDir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(installDir)) { _toasts.Warning(Loc.Get("Toast.InstallPathUnset")); return; }

        var logDir = Path.Combine(installDir, "R5", "Saved", "Logs");
        if (!Directory.Exists(logDir))
        {
            _toasts.Warning(Loc.Get("Toast.LogFolderMissing"));
            return;
        }
        try { Process.Start(new ProcessStartInfo { FileName = logDir, UseShellExecute = true }); }
        catch (Exception ex) { _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex)); }
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
            _toasts.Warning(Loc.Get("Toast.TimeFormatInvalid"));
            return;
        }

        var days = new List<DayOfWeek>();
        if (RestartMon) days.Add(DayOfWeek.Monday);
        if (RestartTue) days.Add(DayOfWeek.Tuesday);
        if (RestartWed) days.Add(DayOfWeek.Wednesday);
        if (RestartThu) days.Add(DayOfWeek.Thursday);
        if (RestartFri) days.Add(DayOfWeek.Friday);
        if (RestartSat) days.Add(DayOfWeek.Saturday);
        if (RestartSun) days.Add(DayOfWeek.Sunday);

        await _settings.UpdateAsync(s =>
        {
            s.ScheduledRestartEnabled = ScheduledRestartEnabled;
            s.DailyRestartTime = DailyRestartTime;
            s.RestartWarnMinutes = Math.Max(0, RestartWarnMinutes);
            // 7 von 7 Tagen aktiv ist semantisch "täglich" → leere Liste speichern.
            s.RestartDays = days.Count == 7 ? new List<DayOfWeek>() : days;
            s.AutoRestartOnHighRamEnabled = AutoRestartOnHighRamEnabled;
            s.AutoRestartRamThresholdPercent = Math.Clamp(AutoRestartRamThresholdPercent, 10, 100);
            s.AutoRestartOnMaxUptimeEnabled = AutoRestartOnMaxUptimeEnabled;
            s.AutoRestartMaxUptimeHours = Math.Max(1, AutoRestartMaxUptimeHours);
        });
        _toasts.Success(Loc.Get("Toast.AutomationSaved"));
    }

    private async Task LoadEventsAsync()
    {
        var list = await _eventLog.ReadRecentAsync(50).ConfigureAwait(false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Events.Clear();
            foreach (var e in list) Events.Add(e);
        });
    }

    private void OnEventAppended(ServerEvent evt) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Events.Insert(0, evt);
            while (Events.Count > 50) Events.RemoveAt(Events.Count - 1);
        });

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _proc.StatusChanged -= OnStatus;
        _proc.LogAppended -= OnLog;
        _eventLog.Appended -= OnEventAppended;
    }
}
