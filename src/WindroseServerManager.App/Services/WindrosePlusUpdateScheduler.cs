using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Prüft im Hintergrund zyklisch auf neue WindrosePlus-Releases.
/// Intervall kommt aus <c>AppSettings.WindrosePlusUpdateCheckIntervalHours</c>
/// (0 = deaktiviert). Fehler werden ignoriert (nur geloggt).
/// </summary>
public sealed class WindrosePlusUpdateScheduler : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollWhenDisabled = TimeSpan.FromMinutes(15);
    private const int MinIntervalHours = 1;
    private const int MaxIntervalHours = 72;

    private readonly IWindrosePlusUpdateService _updater;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindrosePlusUpdateScheduler> _logger;

    public WindrosePlusUpdateScheduler(
        IWindrosePlusUpdateService updater,
        IAppSettingsService settings,
        ILogger<WindrosePlusUpdateScheduler> logger)
    {
        _updater = updater;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WindrosePlusUpdateScheduler started");
        try { await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _settings.Current.WindrosePlusUpdateCheckIntervalHours;

            if (interval <= 0)
            {
                // Deaktiviert — Einstellung regelmäßig neu prüfen, damit User sie live umstellen kann.
                try { await Task.Delay(PollWhenDisabled, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                continue;
            }

            try
            {
                await _updater.CheckAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WindrosePlusUpdateScheduler: Check fehlgeschlagen");
            }

            var clamped = Math.Clamp(interval, MinIntervalHours, MaxIntervalHours);
            try { await Task.Delay(TimeSpan.FromHours(clamped), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        _logger.LogInformation("WindrosePlusUpdateScheduler stopped");
    }
}
