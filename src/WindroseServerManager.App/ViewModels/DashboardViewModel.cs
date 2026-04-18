using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
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
    private readonly IToastService _toasts;
    private readonly INavigationService _nav;
    private readonly System.Timers.Timer _timer;

    [ObservableProperty] private ServerInstallInfo? _installInfo;
    [ObservableProperty] private ServerStatus _status;
    [ObservableProperty] private string _uptimeText = "—";
    [ObservableProperty] private double _hostCpu;
    [ObservableProperty] private double _hostRamPercent;
    [ObservableProperty] private string _hostRamText = "—";
    [ObservableProperty] private string _diskText = "—";
    [ObservableProperty] private double _procCpu;
    [ObservableProperty] private string _procRamText = "—";
    [ObservableProperty] private string? _inviteCode;
    [ObservableProperty] private string _serverName = "—";
    [ObservableProperty] private string? _activeWorldId;
    [ObservableProperty] private string? _activeWorldName;
    [ObservableProperty] private bool _hasActiveWorld;
    [ObservableProperty] private bool _hasRecentCrash;
    [ObservableProperty] private string? _lastCrashPath;

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

    partial void OnInstallInfoChanged(ServerInstallInfo? value)
    {
        OnPropertyChanged(nameof(IsFirstRun));
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
        INavigationService nav,
        IToastService toasts)
    {
        _install = install;
        _proc = proc;
        _metrics = metrics;
        _settings = settings;
        _config = config;
        _nav = nav;
        _toasts = toasts;
        _proc.StatusChanged += OnServerStatusChanged;
        _status = _proc.Status;

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
        _toasts.Success("Pfad kopiert.");
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

            var p = _metrics.GetServerProcessMetrics();
            if (p is not null)
            {
                ProcCpu = p.CpuPercent;
                ProcRamText = FormatGb(p.RamBytes);
                UptimeText = FormatUptime(p.Uptime);
            }
            else
            {
                ProcCpu = 0;
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
        }
        catch { }
    }

    [RelayCommand]
    private async Task CopyInviteCodeAsync()
    {
        if (InviteCode is null) return;
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(InviteCode);
        _toasts.Success($"Invite-Code kopiert: {InviteCode}");
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

    private static string FormatGb(long bytes) =>
        bytes <= 0 ? "—" : $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";

    private static string FormatUptime(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _proc.StatusChanged -= OnServerStatusChanged;
    }
}
