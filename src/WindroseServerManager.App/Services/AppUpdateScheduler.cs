using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Prüft im Hintergrund auf neue Releases: kurz nach App-Start und danach alle 6 Stunden.
/// Fehler werden ignoriert (nur geloggt) — wir wollen die App nicht crashen wenn GitHub mal hakt.
/// </summary>
public sealed class AppUpdateScheduler : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IAppUpdateService _updater;
    private readonly ILogger<AppUpdateScheduler> _logger;

    public AppUpdateScheduler(IAppUpdateService updater, ILogger<AppUpdateScheduler> logger)
    {
        _updater = updater;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppUpdateScheduler started");
        try { await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _updater.CheckAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AppUpdateScheduler: Check fehlgeschlagen");
            }

            try { await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
        _logger.LogInformation("AppUpdateScheduler stopped");
    }
}
