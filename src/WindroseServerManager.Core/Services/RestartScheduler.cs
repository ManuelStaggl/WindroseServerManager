using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Hosted service that triggers a daily graceful restart at the configured local time,
/// but only if the server is currently running and scheduled restarts are enabled.
/// </summary>
public sealed class RestartScheduler : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<RestartScheduler> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _server;

    private DateTime _lastTriggerDate = DateTime.MinValue;

    public RestartScheduler(
        ILogger<RestartScheduler> logger,
        IAppSettingsService settings,
        IServerProcessService server)
    {
        _logger = logger;
        _settings = settings;
        _server = server;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RestartScheduler started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (ShouldTriggerNow(DateTime.Now))
                {
                    await TriggerRestartAsync(stoppingToken).ConfigureAwait(false);
                    _lastTriggerDate = DateTime.Now.Date;
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

    private bool ShouldTriggerNow(DateTime now)
    {
        if (!_settings.Current.ScheduledRestartEnabled) return false;
        if (_server.Status != ServerStatus.Running) return false;

        var timeStr = _settings.Current.DailyRestartTime;
        if (!TimeSpan.TryParse(timeStr, out var target)) return false;

        // Trigger once per local day, within a 2-minute window starting at the target time.
        var todayAt = now.Date + target;
        var windowEnd = todayAt.AddMinutes(2);
        if (now < todayAt || now > windowEnd) return false;

        // Don't retrigger same day.
        return _lastTriggerDate.Date != now.Date;
    }

    private async Task TriggerRestartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Scheduled restart triggering at {Time}", DateTime.Now);
        try
        {
            await _server.StopAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled restart: graceful stop failed");
        }

        // Wait a moment then start back up.
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
