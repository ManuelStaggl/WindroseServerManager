using System.IO.Compression;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class BackupService : IBackupService
{
    private const string SavesRelativeDir = @"R5\Saved";
    private const string AutoPrefix = "auto-";
    private const string ManualPrefix = "manual-";

    private readonly ILogger<BackupService> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerEventLog _eventLog;

    public BackupService(ILogger<BackupService> logger, IAppSettingsService settings, IServerEventLog eventLog)
    {
        _logger = logger;
        _settings = settings;
        _eventLog = eventLog;
    }

    public string GetBackupDir()
    {
        var dir = _settings.Current.BackupDir;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindroseServerManager", "backups");
        }
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string? GetSavesDir()
    {
        var install = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(install)) return null;
        var saves = Path.Combine(install, SavesRelativeDir);
        return Directory.Exists(saves) ? saves : null;
    }

    public async Task<BackupInfo?> CreateBackupAsync(bool isAutomatic, CancellationToken ct = default)
    {
        var saves = GetSavesDir();
        if (saves is null)
        {
            _logger.LogWarning("Cannot backup: saves directory not found at configured install path");
            return null;
        }

        var backupDir = GetBackupDir();
        var serverName = _settings.Current.Servers.FirstOrDefault(s => s.Id == _settings.Current.ActiveServerId)?.Name ?? "Serveur";

        var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{(isAutomatic ? AutoPrefix : ManualPrefix)}{ts}.zip";
        var fullPath = Path.Combine(backupDir, fileName);

        _logger.LogInformation("Creating {Kind} backup: {Path}", isAutomatic ? "automatic" : "manual", fullPath);

        try
        {
            await Task.Run(() =>
                ZipFile.CreateFromDirectory(saves, fullPath, CompressionLevel.Fastest, includeBaseDirectory: true),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup creation failed");
            try { File.Delete(fullPath); } catch { /* ignore */ }
            throw;
        }

        var info = new FileInfo(fullPath);

        _ = _eventLog.AppendAsync(new ServerEvent(
            DateTime.UtcNow, 
            isAutomatic ? ServerEventType.BackupAutomatic : ServerEventType.BackupManual, 
            $"Sauvegarde créée : {fileName}", 
            ServerName: serverName));

        return new BackupInfo(fileName, fullPath, info.CreationTimeUtc, info.Length, isAutomatic);
    }

    public IEnumerable<BackupInfo> ListBackups()
    {
        var dir = GetBackupDir();
        foreach (var path in Directory.EnumerateFiles(dir, "*.zip"))
        {
            var fi = new FileInfo(path);
            var name = fi.Name;
            var isAuto = name.StartsWith(AutoPrefix, StringComparison.OrdinalIgnoreCase);
            yield return new BackupInfo(name, fi.FullName, fi.CreationTimeUtc, fi.Length, isAuto);
        }
    }

    public void DeleteBackup(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var path = Path.Combine(GetBackupDir(), fileName);
        if (!File.Exists(path))
        {
            _logger.LogDebug("Delete requested for missing backup: {Path}", path);
            return;
        }
        File.Delete(path);
        var serverName = _settings.Current.Servers.FirstOrDefault(s => s.Id == _settings.Current.ActiveServerId)?.Name ?? "Serveur";
        _ = _eventLog.AppendAsync(new ServerEvent(
            DateTime.UtcNow, 
            ServerEventType.BackupDeleted, 
            $"Sauvegarde supprimée : {fileName}", 
            ServerName: serverName));

        _logger.LogInformation("Deleted backup {Path}", path);
    }

    public async Task RestoreBackupAsync(string fileName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var saves = GetSavesDir();
        var install = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(install))
            throw new InvalidOperationException("Server-Installationspfad ist nicht gesetzt.");

        var backupPath = Path.Combine(GetBackupDir(), fileName);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup-Datei nicht gefunden", backupPath);

        var targetRoot = Path.Combine(install, SavesRelativeDir);

        // Safety-snapshot the existing saves before we touch them.
        if (Directory.Exists(targetRoot))
        {
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safetyZip = Path.Combine(GetBackupDir(), $"pre-restore-{ts}.zip");
            _logger.LogInformation("Taking safety snapshot before restore: {Path}", safetyZip);
            await Task.Run(() =>
                ZipFile.CreateFromDirectory(targetRoot, safetyZip, CompressionLevel.Fastest, includeBaseDirectory: true),
                ct).ConfigureAwait(false);

            // Now wipe the saves so extract can recreate cleanly
            Directory.Delete(targetRoot, recursive: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);

        _logger.LogInformation("Restoring backup {Src} → {Dst}", backupPath, targetRoot);
        await Task.Run(() =>
        {
            // Our backups include the base dir "Saved" — so extract into the parent of targetRoot.
            var extractParent = Path.GetDirectoryName(targetRoot)!;
            ZipFile.ExtractToDirectory(backupPath, extractParent, overwriteFiles: true);
        }, ct).ConfigureAwait(false);

        var serverName = _settings.Current.Servers.FirstOrDefault(s => s.Id == _settings.Current.ActiveServerId)?.Name ?? "Serveur";
        _ = _eventLog.AppendAsync(new ServerEvent(
            DateTime.UtcNow, 
            ServerEventType.BackupRestored, 
            $"Restauration effectuée : {fileName}", 
            ServerName: serverName));
    }

    public int ApplyRetention()
    {
        var keep = Math.Max(1, _settings.Current.MaxBackupsToKeep);
        var all = ListBackups().ToList();
        if (all.Count <= keep) return 0;

        var excess = all.Count - keep;

        // Lösch-Prioritaet: zuerst die aeltesten automatischen Backups, danach — falls noetig —
        // die aeltesten manuellen, damit die Max-Kappung in jedem Fall gilt (sonst koennte der
        // Disk-Bedarf unbegrenzt wachsen, wenn nur manuelle Backups existieren).
        var autos = all.Where(b => b.IsAutomatic).OrderBy(b => b.CreatedUtc).ToList();
        var manuals = all.Where(b => !b.IsAutomatic).OrderBy(b => b.CreatedUtc).ToList();

        var toDelete = new List<BackupInfo>(excess);
        foreach (var a in autos)
        {
            if (toDelete.Count >= excess) break;
            toDelete.Add(a);
        }
        if (toDelete.Count < excess)
        {
            foreach (var m in manuals)
            {
                if (toDelete.Count >= excess) break;
                toDelete.Add(m);
                _logger.LogInformation("Retention: loesche manuelles Backup {Name} — Max {Max} erreicht und keine automatischen mehr uebrig.",
                    m.FileName, keep);
            }
        }

        var deleted = 0;
        foreach (var b in toDelete)
        {
            try
            {
                File.Delete(b.FullPath);
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retention delete failed for {Path}", b.FullPath);
            }
        }
        _logger.LogInformation("Retention applied: kept {Kept}, deleted {Deleted}", keep, deleted);
        return deleted;
    }
}
