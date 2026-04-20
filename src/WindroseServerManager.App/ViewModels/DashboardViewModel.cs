using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IServerInstallService _install;
    private readonly IServerProcessService _proc;
    private readonly IMetricsService _metrics;
    private readonly IAppSettingsService _settings;
    private readonly IServerConfigService _config;
    private readonly IBackupService _backup;
    private readonly IToastService _toasts;
    private readonly INavigationService _nav;
    private readonly IWindrosePlusService _wplus;
    private readonly IHttpClientFactory _httpFactory;
    private readonly System.Timers.Timer _timer;

    [ObservableProperty] private ServerInstallInfo? _installInfo;
    [ObservableProperty] private ServerStatus _status;
    [ObservableProperty] private string _uptimeText = "—";
    [ObservableProperty] private double _hostCpu;
    [ObservableProperty] private double _hostRamPercent;
    [ObservableProperty] private string _hostRamText = "—";
    [ObservableProperty] private string _diskText = "—";
    [ObservableProperty] private double _diskUsedPercent;
    [ObservableProperty] private double _procCpu;
    [ObservableProperty] private string _procCpuText = "—";
    [ObservableProperty] private string _procRamText = "—";
    [ObservableProperty] private string? _inviteCode;
    [ObservableProperty] private string _serverName = "—";
    [ObservableProperty] private string? _activeWorldId;
    [ObservableProperty] private string? _activeWorldName;
    [ObservableProperty] private bool _hasActiveWorld;
    [ObservableProperty] private bool _hasRecentCrash;
    [ObservableProperty] private string? _lastCrashPath;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _retrofitBannerVisible;
    [ObservableProperty] private RetrofitBannerViewModel? _retrofitBanner;

    // Phase 10 — Health banner (HEALTH-01, HEALTH-02)
    [ObservableProperty] private bool _healthBannerVisible;
    [ObservableProperty] private HealthBannerViewModel? _healthBanner;

    private bool _healthBannerDismissedForSession;
    private bool _lastHealthCheckFailed;
    private DateTime _healthCheckCooldownUntilUtc = DateTime.MinValue;
    private DateTime _healthCheckStartDelayUntilUtc = DateTime.MinValue;
    private ServerStatus _lastObservedStatus = ServerStatus.Stopped;
    private HttpClient? _healthHttpClient;

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

    public bool HasServerName => !string.IsNullOrWhiteSpace(ServerName) && ServerName != "—";

    public string ActiveWorldDisplayName =>
        string.IsNullOrWhiteSpace(ActiveWorldName) ? (ActiveWorldId ?? "—") : ActiveWorldName!;

    public bool ShowActiveWorldIdSubtitle =>
        !string.IsNullOrWhiteSpace(ActiveWorldName)
        && !string.IsNullOrWhiteSpace(ActiveWorldId)
        && !string.Equals(ActiveWorldName, ActiveWorldId, StringComparison.Ordinal);

    partial void OnActiveWorldNameChanged(string? value)
    {
        OnPropertyChanged(nameof(ActiveWorldDisplayName));
        OnPropertyChanged(nameof(ShowActiveWorldIdSubtitle));
    }

    partial void OnActiveWorldIdChanged(string? value)
    {
        OnPropertyChanged(nameof(ActiveWorldDisplayName));
        OnPropertyChanged(nameof(ShowActiveWorldIdSubtitle));
    }

    public bool IsFirstRun => InstallInfo?.IsInstalled != true || string.IsNullOrEmpty(InviteCode);

    public string LastCrashPathDisplay => string.IsNullOrEmpty(LastCrashPath)
        ? string.Empty
        : Loc.Format("Dashboard.Crash.LogFormat", LastCrashPath);

    public string InstalledStatusText =>
        InstallInfo?.IsInstalled == true ? Loc.Get("Status.Installed") : Loc.Get("Status.NotInstalled");

    public string BuildIdText => Loc.Format("Dashboard.BuildFormat", InstallInfo?.BuildId ?? "—");

    public string ProcCpuDisplay => Loc.Format("Dashboard.CpuFormat", ProcCpuText);
    public string ProcRamDisplay => Loc.Format("Dashboard.RamFormat", ProcRamText);
    public string HostCpuDisplay => Loc.Format("Dashboard.HostCpuFormat", HostCpu);
    public string HostRamDisplay => Loc.Format("Dashboard.HostRamFormat", HostRamText);
    public string DiskDisplay => Loc.Format("Dashboard.DiskFormat", DiskText);

    partial void OnLastCrashPathChanged(string? value) => OnPropertyChanged(nameof(LastCrashPathDisplay));
    partial void OnProcCpuTextChanged(string value) => OnPropertyChanged(nameof(ProcCpuDisplay));
    partial void OnProcRamTextChanged(string value) => OnPropertyChanged(nameof(ProcRamDisplay));
    partial void OnHostCpuChanged(double value) => OnPropertyChanged(nameof(HostCpuDisplay));
    partial void OnHostRamTextChanged(string value) => OnPropertyChanged(nameof(HostRamDisplay));
    partial void OnDiskTextChanged(string value) => OnPropertyChanged(nameof(DiskDisplay));

    private void RaiseLocalizedDisplayBindings()
    {
        OnPropertyChanged(nameof(LastCrashPathDisplay));
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(BuildIdText));
        OnPropertyChanged(nameof(ProcCpuDisplay));
        OnPropertyChanged(nameof(ProcRamDisplay));
        OnPropertyChanged(nameof(HostCpuDisplay));
        OnPropertyChanged(nameof(HostRamDisplay));
        OnPropertyChanged(nameof(DiskDisplay));
    }

    partial void OnInstallInfoChanged(ServerInstallInfo? value)
    {
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(BuildIdText));
    }

    partial void OnServerNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasServerName));
    }

    partial void OnInviteCodeChanged(string? value)
    {
        OnPropertyChanged(nameof(IsFirstRun));
    }

    public DashboardViewModel(
        IServerInstallService install,
        IServerProcessService proc,
        IMetricsService metrics,
        IAppSettingsService settings,
        IServerConfigService config,
        IBackupService backup,
        INavigationService nav,
        IToastService toasts,
        ILocalizationService localization,
        IWindrosePlusService wplus,
        IHttpClientFactory httpFactory)
    {
        _install = install;
        _proc = proc;
        _metrics = metrics;
        _settings = settings;
        _config = config;
        _backup = backup;
        _nav = nav;
        _toasts = toasts;
        _wplus = wplus;
        _httpFactory = httpFactory;
        _proc.StatusChanged += OnServerStatusChanged;
        _status = _proc.Status;

        localization.LanguageChanged += RaiseLocalizedDisplayBindings;

        _timer = new System.Timers.Timer(2000);
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();
        CheckRecentCrashes();
    }

    private void CheckRecentCrashes()
    {
        try
        {
            var dir = Program.CrashDirectory;
            if (!Directory.Exists(dir)) return;

            var cutoff = DateTime.Now.AddDays(-7);
            var latest = new DirectoryInfo(dir)
                .GetFiles("crash-*.txt")
                .Where(f => f.LastWriteTime >= cutoff)
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latest is not null)
            {
                LastCrashPath = latest.FullName;
                HasRecentCrash = true;
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task CopyCrashPathAsync()
    {
        if (string.IsNullOrEmpty(LastCrashPath)) return;
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(LastCrashPath);
        _toasts.Success(Loc.Get("Toast.PathCopied"));
    }

    [RelayCommand]
    private void OpenCrashLog()
    {
        if (string.IsNullOrEmpty(LastCrashPath)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastCrashPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { _toasts.Error(ex.Message); }
    }

    [RelayCommand]
    private void DismissCrashWarning()
    {
        HasRecentCrash = false;
    }

    private void OnServerStatusChanged(ServerStatus s) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = s);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        var cfg = _settings.Current;
        try
        {
            if (!string.IsNullOrWhiteSpace(cfg.ServerInstallDir))
                InstallInfo = await _install.GetInstallInfoAsync(cfg.ServerInstallDir, ct);

            var host = await _metrics.GetHostMetricsAsync(cfg.ServerInstallDir, ct);
            HostCpu = host.CpuPercent;
            HostRamPercent = host.RamTotalBytes > 0 ? host.RamUsedBytes * 100.0 / host.RamTotalBytes : 0;
            HostRamText = $"{FormatGb(host.RamUsedBytes)} / {FormatGb(host.RamTotalBytes)}";
            DiskText = $"{FormatGb(host.DiskFreeBytes)} frei / {FormatGb(host.DiskTotalBytes)}";
            DiskUsedPercent = host.DiskTotalBytes > 0
                ? (host.DiskTotalBytes - host.DiskFreeBytes) * 100.0 / host.DiskTotalBytes
                : 0;

            var p = _metrics.GetServerProcessMetrics();
            if (p is not null)
            {
                ProcCpu = p.CpuPercent;
                ProcCpuText = FormatCpu(p.CpuPercent);
                ProcRamText = FormatBytesAuto(p.RamBytes);
                UptimeText = FormatUptime(p.Uptime);
            }
            else
            {
                ProcCpu = 0;
                ProcCpuText = "—";
                ProcRamText = "—";
                UptimeText = "—";
            }

            try
            {
                var desc = await _config.LoadServerDescriptionAsync(ct);
                if (desc is not null)
                {
                    InviteCode = string.IsNullOrWhiteSpace(desc.InviteCode) ? null : desc.InviteCode;
                    ServerName = string.IsNullOrWhiteSpace(desc.ServerName) ? "—" : desc.ServerName;

                    var wid = desc.WorldIslandId;
                    if (!string.IsNullOrWhiteSpace(wid))
                    {
                        ActiveWorldId = wid;
                        try
                        {
                            var w = await _config.LoadWorldDescriptionAsync(wid, ct);
                            ActiveWorldName = string.IsNullOrWhiteSpace(w?.WorldName) ? null : w!.WorldName;
                        }
                        catch { ActiveWorldName = null; }
                        HasActiveWorld = true;
                    }
                    else
                    {
                        ActiveWorldId = null;
                        ActiveWorldName = null;
                        HasActiveWorld = false;
                    }
                }
            }
            catch { }

            OnPropertyChanged(nameof(CanOpenServerDir));
            OnPropertyChanged(nameof(CanOpenServerDescription));

            // Retrofit banner: show when active server has OptInState=NeverAsked and no WP active
            var serverDir = _settings.Current.ServerInstallDir;
            if (!string.IsNullOrWhiteSpace(serverDir))
            {
                var optState = _settings.Current.WindrosePlusOptInStateByServer
                    .GetValueOrDefault(serverDir, OptInState.NeverAsked);
                var wpActive = _settings.Current.WindrosePlusActiveByServer
                    .GetValueOrDefault(serverDir, false);
                var shouldShow = optState == OptInState.NeverAsked && !wpActive;

                // Hide while an install is in progress (Pitfall 7: banner hidden during active install)
                if (shouldShow && RetrofitBanner is { IsInstalling: true })
                    shouldShow = false;

                if (shouldShow)
                {
                    if (RetrofitBanner is null || RetrofitBanner.ServerInstallDir != serverDir)
                    {
                        if (RetrofitBanner is not null)
                            RetrofitBanner.StateChanged -= OnRetrofitStateChanged;
                        RetrofitBanner = new RetrofitBannerViewModel(serverDir, _wplus, _settings, _toasts);
                        RetrofitBanner.StateChanged += OnRetrofitStateChanged;
                    }
                    RetrofitBannerVisible = true;
                }
                else
                {
                    RetrofitBannerVisible = false;
                }

                // Phase 10 — WindrosePlus health check (HEALTH-01)
                // Only relevant when WP is active AND server is running.
                var wpActiveForHealth = _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverDir, false);

                // Detect Stopped/Starting -> Running transition → arm 15s grace period
                if (_lastObservedStatus != ServerStatus.Running && Status == ServerStatus.Running)
                {
                    _healthCheckStartDelayUntilUtc = DateTime.UtcNow.AddSeconds(15);
                    _healthBannerDismissedForSession = false; // new start cycle, let banner fire again
                    _lastHealthCheckFailed = false;
                }
                // Reset on server stop
                if (Status != ServerStatus.Running)
                {
                    _lastHealthCheckFailed = false;
                }
                _lastObservedStatus = Status;

                var nowUtc = DateTime.UtcNow;
                var inGrace = nowUtc < _healthCheckStartDelayUntilUtc;

                if (wpActiveForHealth && Status == ServerStatus.Running && !inGrace && nowUtc >= _healthCheckCooldownUntilUtc)
                {
                    _healthCheckCooldownUntilUtc = nowUtc.AddSeconds(10);
                    var portForHealth = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);
                    _healthHttpClient ??= _httpFactory.CreateClient();
                    _healthHttpClient.Timeout = TimeSpan.FromSeconds(3);
                    _lastHealthCheckFailed = !await HealthCheckHelper.IsHealthyAsync(portForHealth, _healthHttpClient, ct).ConfigureAwait(true);
                }

                var shouldShowHealth = wpActiveForHealth
                    && Status == ServerStatus.Running
                    && !inGrace
                    && _lastHealthCheckFailed
                    && !_healthBannerDismissedForSession;

                if (shouldShowHealth)
                {
                    var portForBanner = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);
                    if (HealthBanner is null || HealthBanner.ServerInstallDir != serverDir || HealthBanner.DashboardPort != portForBanner)
                    {
                        if (HealthBanner is not null)
                            HealthBanner.StateChanged -= OnHealthStateChanged;
                        HealthBanner = new HealthBannerViewModel(serverDir, portForBanner, _wplus, _proc, InstallInfo?.BuildId);
                        HealthBanner.StateChanged += OnHealthStateChanged;
                    }
                    HealthBannerVisible = true;
                }
                else
                {
                    HealthBannerVisible = false;
                }
            }
            else
            {
                RetrofitBannerVisible = false;
                HealthBannerVisible = false;
            }
        }
        catch { }
    }

    private void OnRetrofitStateChanged() => _ = RefreshAsync(CancellationToken.None);

    private void OnHealthStateChanged()
    {
        _healthBannerDismissedForSession = true;
        HealthBannerVisible = false;
    }

    [RelayCommand]
    private async Task CopyInviteCodeAsync()
    {
        if (InviteCode is null) return;
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(InviteCode);
        _toasts.Success(Loc.Format("Toast.InviteCopiedFormat", InviteCode));
    }

    [RelayCommand]
    private void GoToConfiguration()
    {
        var vm = (ConfigurationViewModel)App.Services.GetService(typeof(ConfigurationViewModel))!;
        _nav.NavigateTo(vm);
    }

    [RelayCommand]
    private void GoToInstallation()
    {
        var vm = (InstallationViewModel)App.Services.GetService(typeof(InstallationViewModel))!;
        _nav.NavigateTo(vm);
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
    private async Task StartWithBackupAsync()
    {
        ErrorMessage = _proc.ValidateCanStart();
        if (ErrorMessage is not null) { _toasts.Warning(ErrorMessage); return; }

        try
        {
            IsBusy = true;
            _toasts.Info(Loc.Get("Toast.BackupBeforeStart"));
            var info = await _backup.CreateBackupAsync(isAutomatic: false);
            if (info is not null)
                _toasts.Success(Loc.Format("Toast.BackupCreatedFormat", info.FileName));

            await _proc.StartAsync();
            _toasts.Success(Loc.Get("Toast.ServerStarting"));
        }
        catch (Exception ex)
        {
            var msg = ErrorMessageHelper.FriendlyMessage(ex);
            ErrorMessage = msg;
            _toasts.Error(msg);
        }
        finally
        {
            IsBusy = false;
        }
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
    private void OpenServerDir()
    {
        var path = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenServerDescription()
    {
        var path = _config.GetServerDescriptionPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
    }

    private static string FormatCpu(double percent)
    {
        if (percent <= 0) return "0 %";
        if (percent < 0.1) return "<0.1 %";
        return $"{percent:0.0} %";
    }

    private static string FormatBytesAuto(long bytes)
    {
        if (bytes <= 0) return "—";
        double gb = bytes / 1024.0 / 1024.0 / 1024.0;
        if (gb >= 1.0) return $"{gb:0.00} GB";
        double mb = bytes / 1024.0 / 1024.0;
        return $"{mb:0} MB";
    }

    private static string FormatGb(long bytes) =>
        bytes <= 0 ? "—" : $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";

    private static string FormatUptime(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _proc.StatusChanged -= OnServerStatusChanged;
        if (RetrofitBanner is not null)
            RetrofitBanner.StateChanged -= OnRetrofitStateChanged;
        if (HealthBanner is not null)
            HealthBanner.StateChanged -= OnHealthStateChanged;
        _healthHttpClient?.Dispose();
        _healthHttpClient = null;
    }
}
