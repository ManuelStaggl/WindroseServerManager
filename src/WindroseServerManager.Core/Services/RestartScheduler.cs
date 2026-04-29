using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Triggered sources for scheduled and automatic restarts. Raised on the scheduler thread.
/// </summary>
public sealed record RestartEvent(RestartTrigger Trigger, string Reason);

public enum RestartTrigger { ScheduledTime, HighRam, MaxUptime, ScheduledWarning }

/// <summary>
/// Hosted service that triggers scheduled + threshold-based restarts. Raises RestartNotified
/// for warnings (so the UI can toast) and actual restart events.
/// </summary>
public sealed class RestartScheduler : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<RestartScheduler> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _server;
    private readonly IMetricsService _metrics;
    private readonly IServerEventLog _events;
    private readonly IBackupService _backupService;

    private DateTime _lastTriggerDate = DateTime.MinValue;
    private DateTime _lastWarnDate = DateTime.MinValue;
    private DateTime _lastAutoRestartUtc = DateTime.MinValue;

    public event Action<RestartEvent>? RestartNotified;

    public RestartScheduler(
        ILogger<RestartScheduler> logger,
        IAppSettingsService settings,
        IServerProcessService server,
        IMetricsService metrics,
        IServerEventLog events,
        IBackupService backupService)
    {
        _logger = logger;
        _settings = settings;
        _server = server;
        _metrics = metrics;
        _events = events;
        _backupService = backupService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RestartScheduler started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;

                // Warn-Toast vor geplantem Restart.
                if (ShouldWarnNow(now))
                {
                    var mins = Math.Max(0, _settings.Current.RestartWarnMinutes);
                    RestartNotified?.Invoke(new RestartEvent(
                        RestartTrigger.ScheduledWarning,
                        $"Geplanter Restart in {mins} Minuten."));
                    _lastWarnDate = now.Date;
                }

                // Geplanter Restart zum Zeitpunkt.
                if (ShouldTriggerScheduledNow(now))
                {
                    await TriggerRestartAsync(RestartTrigger.ScheduledTime, "Geplanter Restart.", stoppingToken).ConfigureAwait(false);
                    _lastTriggerDate = now.Date;
                }
                else
                {
                    // Schwellen-Checks — nur wenn Server läuft und nicht gerade nach Auto-Restart.
                    if (_server.Status == ServerStatus.Running
                        && (DateTime.UtcNow - _lastAutoRestartUtc).TotalMinutes >= 5)
                    {
                        var (threshold, reason) = CheckAutoRestartThresholds();
                        if (threshold is not null)
                        {
                            await TriggerRestartAsync(threshold.Value, reason, stoppingToken).ConfigureAwait(false);
                            _lastAutoRestartUtc = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RestartScheduler loop error");
            }

            try { await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        _logger.LogInformation("RestartScheduler stopped");
    }

    private bool IsDayEnabled(DateTime now)
    {
        var days = _settings.Current.RestartDays;
        if (days is null || days.Count == 0) return true; // leer = täglich
        return days.Contains(now.DayOfWeek);
    }

    private bool ShouldTriggerScheduledNow(DateTime now)
    {
        if (!_settings.Current.ScheduledRestartEnabled) return false;
        if (_server.Status != ServerStatus.Running) return false;
        if (!IsDayEnabled(now)) return false;

        var timeStr = _settings.Current.DailyRestartTime;
        if (!TimeSpan.TryParse(timeStr, out var target)) return false;

        var todayAt = now.Date + target;
        var windowEnd = todayAt.AddMinutes(2);
        if (now < todayAt || now > windowEnd) return false;

        return _lastTriggerDate.Date != now.Date;
    }

    private bool ShouldWarnNow(DateTime now)
    {
        if (!_settings.Current.ScheduledRestartEnabled) return false;
        if (_server.Status != ServerStatus.Running) return false;
        if (!IsDayEnabled(now)) return false;

        var warnMins = _settings.Current.RestartWarnMinutes;
        if (warnMins <= 0) return false;

        var timeStr = _settings.Current.DailyRestartTime;
        if (!TimeSpan.TryParse(timeStr, out var target)) return false;

        var todayAt = now.Date + target;
        var warnAt = todayAt.AddMinutes(-warnMins);
        // 2-min Window ab warnAt; einmal pro Tag.
        if (now < warnAt || now > warnAt.AddMinutes(2)) return false;
        return _lastWarnDate.Date != now.Date;
    }

    private (RestartTrigger? trigger, string reason) CheckAutoRestartThresholds()
    {
        var s = _settings.Current;

        if (s.AutoRestartOnMaxUptimeEnabled && _server.StartedAtUtc is not null)
        {
            var uptime = DateTime.UtcNow - _server.StartedAtUtc.Value;
            if (uptime.TotalHours >= Math.Max(1, s.AutoRestartMaxUptimeHours))
                return (RestartTrigger.MaxUptime, $"Uptime-Grenze erreicht ({(int)uptime.TotalHours}h).");
        }

        if (s.AutoRestartOnHighRamEnabled)
        {
            try
            {
                var host = _metrics.GetHostMetricsAsync().GetAwaiter().GetResult();
                var proc = _metrics.GetServerProcessMetrics();
                if (proc is not null && host.RamTotalBytes > 0)
                {
                    var pct = proc.RamBytes * 100.0 / host.RamTotalBytes;
                    if (pct >= Math.Max(10, s.AutoRestartRamThresholdPercent))
                        return (RestartTrigger.HighRam, $"RAM-Grenze erreicht ({pct:F0} %).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Threshold-Check RAM fehlgeschlagen");
            }
        }

        return (null, string.Empty);
    }

    private async Task TriggerRestartAsync(RestartTrigger trigger, string reason, CancellationToken ct)
    {
        _logger.LogInformation("Restart trigger={Trigger} reason={Reason}", trigger, reason);
        RestartNotified?.Invoke(new RestartEvent(trigger, reason));

        var eventType = trigger switch
        {
            RestartTrigger.ScheduledTime => ServerEventType.ScheduledRestart,
            RestartTrigger.HighRam => ServerEventType.AutoRestartHighRam,
            RestartTrigger.MaxUptime => ServerEventType.AutoRestartMaxUptime,
            _ => ServerEventType.ScheduledRestart,
        };
        await _events.AppendAsync(new ServerEvent(DateTime.UtcNow, eventType, reason), ct).ConfigureAwait(false);

        try { await _server.StopAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogError(ex, "Scheduled restart: stop failed"); }

        // Backup on restart if enabled.
        if (_settings.Current.BackupOnRestartEnabled)
        {
            try
            {
                _logger.LogInformation("Creating backup before restart");
                await _backupService.CreateBackupAsync(isAutomatic: true, ct).ConfigureAwait(false);
                _logger.LogInformation("Backup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup on restart failed, but proceeding with restart");
            }
        }

        try { await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        try
        {
            await _server.StartAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Scheduled restart complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled restart: start failed");
        }
    }
}
