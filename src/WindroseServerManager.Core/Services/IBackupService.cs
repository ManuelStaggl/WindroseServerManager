using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IBackupService
{
    /// <summary>Returns the absolute backup directory (creates if needed).</summary>
    string GetBackupDir();

    /// <summary>Returns the absolute saves directory under the server install dir, or null.</summary>
    string? GetSavesDir();

    /// <summary>Creates a backup zip of the saves dir. Returns the created backup, or null if saves dir missing.</summary>
    Task<BackupInfo?> CreateBackupAsync(bool isAutomatic, CancellationToken ct = default);

    IEnumerable<BackupInfo> ListBackups();

    void DeleteBackup(string fileName);

    /// <summary>
    /// Restores a backup to the saves dir. REQUIRES server to be stopped — caller must check.
    /// Existing saves are moved to a safety-snapshot named "pre-restore-{timestamp}.zip".
    /// </summary>
    Task RestoreBackupAsync(string fileName, CancellationToken ct = default);

    /// <summary>Applies retention: keeps only the newest N backups, deletes older ones.</summary>
    int ApplyRetention();
}
