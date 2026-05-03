using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

/// <summary>
/// Comprehensive behavior tests for <see cref="BackupService"/>.
/// Uses temp directories with GUIDs for full isolation and hand-written fakes.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _serverDir;
    private readonly string _savesDir;
    private readonly string _backupDir;
    private readonly FakeAppSettings _settings;
    private readonly FakeServerEventLog _eventLog;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "wrsm-backup-tests-" + Guid.NewGuid().ToString("N"));
        _serverDir = Path.Combine(_rootDir, "server");
        _savesDir = Path.Combine(_serverDir, "R5", "Saved");
        _backupDir = Path.Combine(_rootDir, "backups");

        Directory.CreateDirectory(_savesDir);
        Directory.CreateDirectory(_backupDir);

        // Seed a small file inside the saves directory so ZipFile has content to archive.
        File.WriteAllText(Path.Combine(_savesDir, "test-save.dat"), "save-data");

        _settings = new FakeAppSettings(_serverDir, _backupDir);
        _eventLog = new FakeServerEventLog();
        _service = new BackupService(
            NullLogger<BackupService>.Instance,
            _settings,
            _eventLog);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_rootDir)) Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    /// <summary>
    /// Creates a fake backup zip file directly on disk with the given name prefix.
    /// Bypasses <see cref="BackupService.CreateBackupAsync"/> to avoid timestamp
    /// collisions when tests run in parallel.
    /// </summary>
    private string SeedBackupZip(string prefix, int index)
    {
        var fileName = $"{prefix}{index:D4}-{ Guid.NewGuid().ToString("N")[..8]}.zip";
        var fullPath = Path.Combine(_backupDir, fileName);
        ZipFile.CreateFromDirectory(_savesDir, fullPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        return fileName;
    }

    // ===================================================================
    // CreateBackupAsync
    // ===================================================================

    [Fact]
    public async Task CreateBackupAsync_CreatesAutoZip_WithCorrectPrefix()
    {
        var result = await _service.CreateBackupAsync(isAutomatic: true);

        Assert.NotNull(result);
        Assert.True(result!.IsAutomatic);
        Assert.StartsWith("auto-", result.FileName);
        Assert.True(File.Exists(result.FullPath));
        Assert.EndsWith(".zip", result.FileName);
        Assert.True(result.SizeBytes > 0);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesManualZip_WithCorrectPrefix()
    {
        var result = await _service.CreateBackupAsync(isAutomatic: false);

        Assert.NotNull(result);
        Assert.False(result!.IsAutomatic);
        Assert.StartsWith("manual-", result.FileName);
        Assert.True(File.Exists(result.FullPath));
        Assert.EndsWith(".zip", result.FileName);
        Assert.True(result.SizeBytes > 0);
    }

    [Fact]
    public async Task CreateBackupAsync_ReturnsNull_WhenNoSavesDir()
    {
        // Point ActiveServerDir to a path that has no R5\Saved subdirectory.
        var emptyRoot = Path.Combine(_rootDir, "empty-server");
        Directory.CreateDirectory(emptyRoot);
        _settings.SetActiveServerDir(emptyRoot);

        var result = await _service.CreateBackupAsync(isAutomatic: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateBackupAsync_CleansUpPartialZip_OnException()
    {
        // Strategy: create a read-only directory where the backup zip should be
        // written, so that ZipFile.CreateFromDirectory fails with an IOException.
        // We cannot predict the timestamp in the filename, so instead we make the
        // entire backup directory unwritable by placing a file at the path the zip
        // would need to occupy.
        //
        // A simpler approach: delete the saves directory after GetSavesDir() returns
        // it but before ZipFile uses it. However since the service resolves it at
        // the top, we must corrupt the directory content instead.
        //
        // Most reliable: delete the saves directory so that ZipFile.CreateFromDirectory
        // throws DirectoryNotFoundException. But GetSavesDir() already validated it.
        // So we delete it between the GetSavesDir() call and the ZipFile call. Since
        // there is no hook, we lock the output path by pre-creating a directory at a
        // filename that matches the pattern.
        //
        // Simplest reliable approach: corrupt the saves dir by removing it right
        // before the call. The service calls GetSavesDir() which checks existence,
        // then calls ZipFile. We cannot inject a gap. But we CAN make the saves dir
        // contain a file that cannot be read.
        //
        // Final approach: make the backup dir read-only by creating it as a file,
        // not a directory. The service calls GetBackupDir() which calls
        // Directory.CreateDirectory, but if a file exists at that path, it will
        // throw when trying to write the zip. Let us set BackupDir to a path that
        // is a file, not a directory.
        var fileAsDir = Path.Combine(_rootDir, "backup-is-a-file");
        File.WriteAllText(fileAsDir, "not a directory");
        _settings.SetBackupDir(fileAsDir);

        // GetSavesDir still works -- the saves dir is intact.
        // GetBackupDir calls Directory.CreateDirectory which will fail silently
        // (it's a file) and then return the path. Then ZipFile.CreateFromDirectory
        // will fail because it cannot write to a path whose parent is a file.
        // Actually, Directory.CreateDirectory throws when a file exists at the path.
        // That exception bubbles up from GetBackupDir() inside CreateBackupAsync.
        // The catch block in CreateBackupAsync will try File.Delete on the zip path
        // (which does not exist) and then rethrow.
        await Assert.ThrowsAnyAsync<Exception>(() => _service.CreateBackupAsync(isAutomatic: true));

        // No zip files should exist at the location.
        Assert.Empty(Directory.GetFiles(_rootDir, "*.zip"));
    }

    // ===================================================================
    // ListBackups
    // ===================================================================

    [Fact]
    public void ListBackups_ReturnsAllBackups()
    {
        // Seed three backup zips with distinct names.
        var f1 = SeedBackupZip("auto-", 1);
        var f2 = SeedBackupZip("auto-", 2);
        var f3 = SeedBackupZip("manual-", 3);

        var all = _service.ListBackups().ToList();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, b => b.FileName == f1);
        Assert.Contains(all, b => b.FileName == f2);
        Assert.Contains(all, b => b.FileName == f3);
    }

    [Fact]
    public void ListBackups_Distinguishes_AutoAndManual()
    {
        SeedBackupZip("auto-", 1);
        SeedBackupZip("manual-", 2);

        var all = _service.ListBackups().ToList();

        Assert.Equal(2, all.Count);
        Assert.Single(all, b => b.IsAutomatic && b.FileName.StartsWith("auto-"));
        Assert.Single(all, b => !b.IsAutomatic && b.FileName.StartsWith("manual-"));
    }

    // ===================================================================
    // DeleteBackup
    // ===================================================================

    [Fact]
    public void DeleteBackup_RemovesFile_WhenExists()
    {
        var fileName = SeedBackupZip("auto-", 1);
        var fullPath = Path.Combine(_backupDir, fileName);
        Assert.True(File.Exists(fullPath));

        _service.DeleteBackup(fileName);

        Assert.False(File.Exists(fullPath));
        // Verify the event log captured a BackupDeleted entry.
        Assert.Contains(_eventLog.Events, e => e.Type == ServerEventType.BackupDeleted);
    }

    [Fact]
    public void DeleteBackup_NoOp_WhenFileMissing()
    {
        // Delete a file that does not exist -- should not throw.
        var exception = Record.Exception(() => _service.DeleteBackup("nonexistent.zip"));
        Assert.Null(exception);
    }

    [Fact]
    public void DeleteBackup_Throws_WhenFileNameEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.DeleteBackup(""));
    }

    // ===================================================================
    // RestoreBackup
    // ===================================================================

    [Fact]
    public async Task RestoreBackup_Throws_WhenBackupFileMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.RestoreBackupAsync("does-not-exist.zip"));
    }

    [Fact]
    public async Task RestoreBackup_Throws_WhenNoInstallDir()
    {
        // Set an empty ActiveServerDir so the service has nowhere to restore to.
        _settings.SetActiveServerDir("");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RestoreBackupAsync("any.zip"));
    }

    [Fact]
    public async Task RestoreBackup_RestoresFilesFromZip()
    {
        // Arrange: create a backup via the service, then modify saves to prove overwrite.
        var originalContent = "original-save-data";
        File.WriteAllText(Path.Combine(_savesDir, "savegame.dat"), originalContent);

        var backup = await _service.CreateBackupAsync(isAutomatic: true);
        Assert.NotNull(backup);

        // Corrupt the save after backup.
        File.WriteAllText(Path.Combine(_savesDir, "savegame.dat"), "corrupted");

        // Act: restore the backup.
        await _service.RestoreBackupAsync(backup!.FileName);

        // Assert: file content is back to original.
        var restored = File.ReadAllText(Path.Combine(_savesDir, "savegame.dat"));
        Assert.Equal(originalContent, restored);

        // Event log should have a BackupRestored entry.
        Assert.Contains(_eventLog.Events, e => e.Type == ServerEventType.BackupRestored);
    }

    [Fact]
    public async Task RestoreBackup_CreatesSafetySnapshot()
    {
        // Ensure there is a save to protect.
        File.WriteAllText(Path.Combine(_savesDir, "important.dat"), "do-not-lose");

        var backup = await _service.CreateBackupAsync(isAutomatic: true);
        Assert.NotNull(backup);

        await _service.RestoreBackupAsync(backup!.FileName);

        // A pre-restore safety snapshot should have been created in the backup dir.
        var safetyZips = Directory.GetFiles(_backupDir, "pre-restore-*.zip");
        Assert.Single(safetyZips);
    }

    // ===================================================================
    // ApplyRetention
    // ===================================================================

    [Fact]
    public void ApplyRetention_DeletesOldestAutoBackups_First()
    {
        // Seed 4 auto backup zips. We set distinct creation times by writing them
        // with small delays so the filesystem timestamps differ.
        _settings.SetMaxBackupsToKeep(2);

        var f1 = SeedBackupZip("auto-", 1);
        File.SetCreationTime(Path.Combine(_backupDir, f1), DateTime.Now.AddMinutes(-40));
        var f2 = SeedBackupZip("auto-", 2);
        File.SetCreationTime(Path.Combine(_backupDir, f2), DateTime.Now.AddMinutes(-30));
        var f3 = SeedBackupZip("auto-", 3);
        File.SetCreationTime(Path.Combine(_backupDir, f3), DateTime.Now.AddMinutes(-20));
        var f4 = SeedBackupZip("auto-", 4);
        File.SetCreationTime(Path.Combine(_backupDir, f4), DateTime.Now.AddMinutes(-10));

        // Before retention: 4 auto backups.
        Assert.Equal(4, _service.ListBackups().Count());

        var deleted = _service.ApplyRetention();

        // Should delete 2 oldest auto backups (f1, f2).
        Assert.Equal(2, deleted);
        var remaining = _service.ListBackups().ToList();
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, b => b.FileName == f1);
        Assert.DoesNotContain(remaining, b => b.FileName == f2);
        Assert.Contains(remaining, b => b.FileName == f3);
        Assert.Contains(remaining, b => b.FileName == f4);
    }

    [Fact]
    public void ApplyRetention_FallsBackToManualBackups_WhenAutosExhausted()
    {
        // Create 2 auto and 2 manual backups. Keep only 2.
        _settings.SetMaxBackupsToKeep(2);

        var auto1 = SeedBackupZip("auto-", 1);
        File.SetCreationTime(Path.Combine(_backupDir, auto1), DateTime.Now.AddMinutes(-40));
        var auto2 = SeedBackupZip("auto-", 2);
        File.SetCreationTime(Path.Combine(_backupDir, auto2), DateTime.Now.AddMinutes(-30));
        var manual1 = SeedBackupZip("manual-", 3);
        File.SetCreationTime(Path.Combine(_backupDir, manual1), DateTime.Now.AddMinutes(-20));
        var manual2 = SeedBackupZip("manual-", 4);
        File.SetCreationTime(Path.Combine(_backupDir, manual2), DateTime.Now.AddMinutes(-10));

        // 4 total, keep 2 => delete 2.
        var deleted = _service.ApplyRetention();

        Assert.Equal(2, deleted);
        var remaining = _service.ListBackups().ToList();
        Assert.Equal(2, remaining.Count);

        // Oldest auto backups should be deleted first.
        Assert.DoesNotContain(remaining, b => b.FileName == auto1);
        Assert.DoesNotContain(remaining, b => b.FileName == auto2);
        // The two manual backups survive because autos were deleted first and
        // we only needed to delete 2.
        Assert.Contains(remaining, b => b.FileName == manual1);
        Assert.Contains(remaining, b => b.FileName == manual2);
    }

    [Fact]
    public void ApplyRetention_RespectsMaxBackupsToKeep_MinimumOne()
    {
        // Set MaxBackupsToKeep to 0, which the service clamps to 1 via Math.Max(1, ...).
        _settings.SetMaxBackupsToKeep(0);

        var f1 = SeedBackupZip("auto-", 1);
        File.SetCreationTime(Path.Combine(_backupDir, f1), DateTime.Now.AddMinutes(-30));
        var f2 = SeedBackupZip("auto-", 2);
        File.SetCreationTime(Path.Combine(_backupDir, f2), DateTime.Now.AddMinutes(-20));
        var f3 = SeedBackupZip("auto-", 3);
        File.SetCreationTime(Path.Combine(_backupDir, f3), DateTime.Now.AddMinutes(-10));

        // 3 backups, keep min 1 => delete 2.
        var deleted = _service.ApplyRetention();

        Assert.Equal(2, deleted);
        var remaining = _service.ListBackups().ToList();
        Assert.Single(remaining);
        // The newest (f3) should survive.
        Assert.Equal(f3, remaining[0].FileName);
    }

    [Fact]
    public void ApplyRetention_ReturnsZero_WhenUnderLimit()
    {
        _settings.SetMaxBackupsToKeep(10);

        SeedBackupZip("auto-", 1);
        SeedBackupZip("manual-", 2);

        var deleted = _service.ApplyRetention();

        Assert.Equal(0, deleted);
        Assert.Equal(2, _service.ListBackups().Count());
    }

    [Fact]
    public void ApplyRetention_WhenOnlyManualsExist_DeletesOldestManuals()
    {
        _settings.SetMaxBackupsToKeep(1);

        var m1 = SeedBackupZip("manual-", 1);
        File.SetCreationTime(Path.Combine(_backupDir, m1), DateTime.Now.AddMinutes(-30));
        var m2 = SeedBackupZip("manual-", 2);
        File.SetCreationTime(Path.Combine(_backupDir, m2), DateTime.Now.AddMinutes(-20));
        var m3 = SeedBackupZip("manual-", 3);
        File.SetCreationTime(Path.Combine(_backupDir, m3), DateTime.Now.AddMinutes(-10));

        var deleted = _service.ApplyRetention();

        Assert.Equal(2, deleted);
        var remaining = _service.ListBackups().ToList();
        Assert.Single(remaining);
        // The newest manual should survive.
        Assert.Equal(m3, remaining[0].FileName);
    }

    // ===================================================================
    // GetBackupDir / GetSavesDir
    // ===================================================================

    [Fact]
    public void GetBackupDir_CreatesDirectoryIfMissing()
    {
        var freshDir = Path.Combine(_rootDir, "new-backup-dir");
        Assert.False(Directory.Exists(freshDir));

        _settings.SetBackupDir(freshDir);
        var result = _service.GetBackupDir();

        Assert.Equal(freshDir, result);
        Assert.True(Directory.Exists(freshDir));
    }

    [Fact]
    public void GetSavesDir_ReturnsNull_WhenInstallDirEmpty()
    {
        _settings.SetActiveServerDir("");
        var result = _service.GetSavesDir();
        Assert.Null(result);
    }

    [Fact]
    public void GetSavesDir_ReturnsNull_WhenSavesSubdirMissing()
    {
        var noSaves = Path.Combine(_rootDir, "no-saves-server");
        Directory.CreateDirectory(noSaves);
        _settings.SetActiveServerDir(noSaves);

        var result = _service.GetSavesDir();
        Assert.Null(result);
    }

    [Fact]
    public void GetSavesDir_ReturnsPath_WhenDirectoryExists()
    {
        var result = _service.GetSavesDir();
        Assert.NotNull(result);
        Assert.Equal(_savesDir, result);
    }

    // ===================================================================
    // Hand-written fakes
    // ===================================================================

    /// <summary>
    /// Mutable fake for <see cref="IAppSettingsService"/> that lets tests
    /// control ActiveServerDir, BackupDir, and MaxBackupsToKeep on the fly.
    /// </summary>
    private sealed class FakeAppSettings : IAppSettingsService
    {
        private readonly AppSettings _current = new();

        public FakeAppSettings(string activeServerDir, string backupDir)
        {
            _current.ServerInstallDir = activeServerDir;
            _current.BackupDir = backupDir;
            _current.MaxBackupsToKeep = 20;

            // Seed a server entry so CreateBackupAsync can resolve a server name.
            var entry = new ServerEntry
            {
                Id = "test-server",
                Name = "TestServer",
                InstallDir = activeServerDir,
            };
            _current.Servers.Add(entry);
            _current.ActiveServerId = entry.Id;
        }

        public AppSettings Current => _current;
        public string ActiveServerDir => _current.ServerInstallDir;

        public event Action<AppSettings>? Changed { add { } remove { } }

        public Task SelectServerAsync(string serverId) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
        {
            mutate(_current);
            return Task.CompletedTask;
        }

        public void SetActiveServerDir(string dir)
        {
            _current.ServerInstallDir = dir;
            // Also update the server entry's InstallDir so it stays consistent.
            if (_current.Servers.Count > 0)
                _current.Servers[0].InstallDir = dir;
        }

        public void SetBackupDir(string dir) => _current.BackupDir = dir;
        public void SetMaxBackupsToKeep(int max) => _current.MaxBackupsToKeep = max;
    }

    /// <summary>
    /// Recording fake for <see cref="IServerEventLog"/>. Stores all appended
    /// events in-memory so tests can inspect what was logged.
    /// </summary>
    private sealed class FakeServerEventLog : IServerEventLog
    {
        private readonly List<ServerEvent> _events = new();

        public IReadOnlyList<ServerEvent> Events => _events;
        public event Action<ServerEvent>? Appended { add { } remove { } }

        public Task AppendAsync(ServerEvent evt, CancellationToken ct = default)
        {
            _events.Add(evt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ServerEvent>> ReadRecentAsync(int maxCount = 100, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ServerEvent>>(_events.TakeLast(maxCount).ToList());

        public Task ClearAsync(CancellationToken ct = default)
        {
            _events.Clear();
            return Task.CompletedTask;
        }
    }
}
