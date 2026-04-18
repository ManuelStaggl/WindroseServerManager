using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Hosted service that triggers an automatic backup every AutoBackupIntervalMinutes
/// while the Windrose server is running and AutoBackupEnabled is true.
/// </summary>
public sealed class BackupScheduler : BackgroundService
{
    private readonly ILogger<BackupScheduler> _logger;
    private readonly IBackupService _backups;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _server;

    public BackupScheduler(
        ILogger<BackupScheduler> logger,
        IBackupService backups,
        IAppSettingsService settings,
        IServerProcessService server)
    {
        _logger = logger;
        _backups = backups;
        _settings = settings;
        _server = server;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupScheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = Math.Max(5, _settings.Current.AutoBackupIntervalMinutes);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            if (!_settings.Current.AutoBackupEnabled) continue;
            if (_server.Status != ServerStatus.Running) continue;

            try
            {
                var info = await _backups.CreateBackupAsync(isAutomatic: true, stoppingToken).ConfigureAwait(false);
                if (info is not null)
                {
                    _logger.LogInformation("Auto-backup created: {File} ({Size} bytes)", info.FileName, info.SizeBytes);
                    _backups.ApplyRetention();
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-backup failed");
            }
        }

        _logger.LogInformation("BackupScheduler stopped");
    }
}
