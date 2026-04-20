using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class HealthBannerViewModel : ViewModelBase
{
    private readonly IWindrosePlusService _wplus;
    private readonly IServerProcessService _proc;
    private readonly string? _windroseBuildId;

    public string ServerInstallDir { get; }
    public int DashboardPort { get; }

    /// <summary>
    /// Raised after Dismiss so DashboardViewModel can hide the banner immediately
    /// without waiting for the next timer tick.
    /// </summary>
    public event Action? StateChanged;

    /// <param name="serverInstallDir">Full path of the server install directory.</param>
    /// <param name="dashboardPort">WindrosePlus HTTP dashboard port.</param>
    /// <param name="wplus">WindrosePlus service, used to read the version marker.</param>
    /// <param name="proc">Server process service, used to read the recent log tail.</param>
    /// <param name="windroseBuildId">BuildId of the installed Windrose server (for the report URL). Pass null if unknown.</param>
    public HealthBannerViewModel(
        string serverInstallDir,
        int dashboardPort,
        IWindrosePlusService wplus,
        IServerProcessService proc,
        string? windroseBuildId = null)
    {
        ServerInstallDir = serverInstallDir;
        DashboardPort = dashboardPort;
        _wplus = wplus;
        _proc = proc;
        _windroseBuildId = windroseBuildId;
    }

    [RelayCommand]
    private void Dismiss() => StateChanged?.Invoke();

    [RelayCommand]
    private void OpenReport()
    {
        try
        {
            var url = BuildReportUrl();
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open WindrosePlus report URL for {Dir}", ServerInstallDir);
        }
    }

    private string BuildReportUrl()
    {
        var wpVersion = _wplus.ReadVersionMarker(ServerInstallDir)?.Tag ?? "unknown";
        var windVer = string.IsNullOrWhiteSpace(_windroseBuildId) ? "unknown" : _windroseBuildId;
        var logTail = BuildLogTailLines();
        return ReportUrlBuilder.Build(windVer, wpVersion, DashboardPort, logTail);
    }

    private IReadOnlyList<string> BuildLogTailLines()
    {
        var lines = _proc.RecentLog;
        if (lines is null || lines.Count == 0)
            return Array.Empty<string>();
        return lines
            .Skip(Math.Max(0, lines.Count - 20))
            .Select(l => $"[{l.TimestampUtc:HH:mm:ss}] [{l.Stream}] {l.Text}")
            .ToList();
    }
}
