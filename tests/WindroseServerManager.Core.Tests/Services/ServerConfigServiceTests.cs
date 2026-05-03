using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

public sealed class ServerConfigServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _installDir;
    private readonly FakeAppSettingsService _settings;
    private readonly ServerConfigService _sut;

    public ServerConfigServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"wsm-test-{Guid.NewGuid():N}");
        _installDir = Path.Combine(_tempRoot, "Server");
        _settings = new FakeAppSettingsService(_installDir);
        _sut = new ServerConfigService(NullLogger<ServerConfigService>.Instance, _settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ---------------------------------------------------------------
    // 1. LoadServerDescriptionAsync — file missing → null
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadServerDescriptionAsync_ReturnsNull_WhenFileMissing()
    {
        Directory.CreateDirectory(_installDir);

        var result = await _sut.LoadServerDescriptionAsync();

        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // 2. LoadServerDescriptionAsync — valid JSON parsed correctly
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadServerDescriptionAsync_ParsesValidJson()
    {
        Directory.CreateDirectory(_installDir);
        var json = """{"Version":1,"DeploymentId":"0.10.0.0.251-master-abc","ServerDescription_Persistent":{"ServerName":"Test","MaxPlayerCount":10}}""";
        await WriteServerDescriptionFile(json);

        var result = await _sut.LoadServerDescriptionAsync();

        Assert.NotNull(result);
        Assert.Equal("Test", result!.ServerName);
        Assert.Equal(10, result.MaxPlayerCount);
    }

    // ---------------------------------------------------------------
    // 3. SaveServerDescriptionAsync — creates R5 dir if missing
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveServerDescriptionAsync_CreatesDirectory_IfMissing()
    {
        // Only create the root install dir, not R5 subdirectory.
        Directory.CreateDirectory(_installDir);

        var desc = new ServerDescription { ServerName = "CreatedDir", MaxPlayerCount = 4 };
        await _sut.SaveServerDescriptionAsync(desc);

        var r5Dir = Path.Combine(_installDir, "R5");
        Assert.True(Directory.Exists(r5Dir));
        Assert.True(File.Exists(Path.Combine(r5Dir, "ServerDescription.json")));
    }

    // ---------------------------------------------------------------
    // 4. SaveServerDescriptionAsync — atomic write (no .tmp remains)
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveServerDescriptionAsync_WritesAtomicTmpThenMove()
    {
        Directory.CreateDirectory(_installDir);

        var desc = new ServerDescription { ServerName = "Atomic" };
        await _sut.SaveServerDescriptionAsync(desc);

        var expectedPath = Path.Combine(_installDir, "R5", "ServerDescription.json");
        Assert.True(File.Exists(expectedPath));
        Assert.False(File.Exists(expectedPath + ".tmp"));
    }

    // ---------------------------------------------------------------
    // 5. SaveServerDescriptionAsync — preserves envelope fields
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveServerDescriptionAsync_PreservesExistingEnvelopeFields()
    {
        Directory.CreateDirectory(_installDir);

        // Pre-write a complete envelope with Version and DeploymentId.
        var originalJson = """{"Version":2,"DeploymentId":"0.10.0.0.251-master-abc","ServerDescription_Persistent":{"ServerName":"Old","MaxPlayerCount":5}}""";
        await WriteServerDescriptionFile(originalJson);

        // Load to populate the internal envelope cache.
        await _sut.LoadServerDescriptionAsync();

        // Now save new persistent data — envelope fields must survive.
        var updated = new ServerDescription { ServerName = "New", MaxPlayerCount = 20 };
        await _sut.SaveServerDescriptionAsync(updated);

        // Read raw bytes and verify the envelope is intact.
        var raw = await File.ReadAllTextAsync(Path.Combine(_installDir, "R5", "ServerDescription.json"));
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("Version", out var versionEl));
        Assert.Equal(2, versionEl.GetInt32());

        Assert.True(root.TryGetProperty("DeploymentId", out var deployEl));
        Assert.Equal("0.10.0.0.251-master-abc", deployEl.GetString());

        Assert.True(root.TryGetProperty("ServerDescription_Persistent", out var persistEl));
        Assert.Equal("New", persistEl.GetProperty("ServerName").GetString());
        Assert.Equal(20, persistEl.GetProperty("MaxPlayerCount").GetInt32());
    }

    // ---------------------------------------------------------------
    // 6. SaveServerDescriptionAsync — throws when no install dir
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveServerDescriptionAsync_Throws_WhenNoInstallDir()
    {
        // Point ActiveServerDir to empty string (no server configured).
        var emptySettings = new FakeAppSettingsService(string.Empty);
        var sut = new ServerConfigService(NullLogger<ServerConfigService>.Instance, emptySettings);

        var desc = new ServerDescription { ServerName = "Nowhere" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SaveServerDescriptionAsync(desc));
    }

    // ---------------------------------------------------------------
    // 7. ListWorldIds — empty when no Worlds dir exists
    // ---------------------------------------------------------------
    [Fact]
    public void ListWorldIds_ReturnsEmpty_WhenNoWorldsDir()
    {
        Directory.CreateDirectory(_installDir);

        var result = _sut.ListWorldIds();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------
    // 8. ListWorldIds — returns island IDs from directory names
    // ---------------------------------------------------------------
    [Fact]
    public void ListWorldIds_ReturnsIslandIds_FromDirectoryNames()
    {
        var worldsRoot = CreateWorldsRoot("0.10.0");
        Directory.CreateDirectory(Path.Combine(worldsRoot, "island-alpha"));
        Directory.CreateDirectory(Path.Combine(worldsRoot, "island-beta"));
        Directory.CreateDirectory(Path.Combine(worldsRoot, "island-gamma"));

        var result = _sut.ListWorldIds().ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains("island-alpha", result);
        Assert.Contains("island-beta", result);
        Assert.Contains("island-gamma", result);
    }

    // ---------------------------------------------------------------
    // 9. LoadWorldDescriptionAsync — parses valid JSON
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadWorldDescriptionAsync_ParsesValidJson()
    {
        const string islandId = "island-test";
        var worldDir = CreateWorldDirectory("0.10.0", islandId);

        var json = """{"Version":1,"WorldDescription":{"islandId":"island-test","WorldName":"Pirate Cove","CreationTime":638500000000000000,"WorldPresetType":"Medium"}}""";
        await File.WriteAllTextAsync(Path.Combine(worldDir, "WorldDescription.json"), json);

        var result = await _sut.LoadWorldDescriptionAsync(islandId);

        Assert.NotNull(result);
        Assert.Equal("island-test", result!.IslandId);
        Assert.Equal("Pirate Cove", result.WorldName);
        Assert.Equal(WorldPresetType.Medium, result.WorldPresetType);
    }

    // ---------------------------------------------------------------
    // 10. SaveWorldDescriptionAsync — auto-creates Worlds dir
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveWorldDescriptionAsync_AutoCreatesWorldsDir()
    {
        Directory.CreateDirectory(_installDir);
        // No RocksDB directory exists yet. The service should create it.

        var world = new WorldDescription { WorldName = "Auto Created" };
        await _sut.SaveWorldDescriptionAsync("island-new", world);

        // The default version path: R5/Saved/SaveProfiles/Default/RocksDB/0.10.0/Worlds/island-new/
        var worldDir = Path.Combine(_installDir, "R5", "Saved", "SaveProfiles", "Default",
            "RocksDB", "0.10.0", "Worlds", "island-new");
        Assert.True(Directory.Exists(worldDir));
        Assert.True(File.Exists(Path.Combine(worldDir, "WorldDescription.json")));
    }

    // ---------------------------------------------------------------
    // 11. SaveWorldDescriptionAsync — sets IslandId from folder name
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveWorldDescriptionAsync_SetsIslandIdFromFolderName()
    {
        var worldsRoot = CreateWorldsRoot("0.10.0");

        var world = new WorldDescription { IslandId = "wrong-id", WorldName = "Test" };
        await _sut.SaveWorldDescriptionAsync("correct-island", world);

        // The service should have overwritten IslandId to match the folder name.
        Assert.Equal("correct-island", world.IslandId);

        // Verify the persisted file also has the corrected IslandId.
        var result = await _sut.LoadWorldDescriptionAsync("correct-island");
        Assert.NotNull(result);
        Assert.Equal("correct-island", result!.IslandId);
    }

    // ---------------------------------------------------------------
    // 12. SaveWorldDescriptionAsync — sets CreationTime when zero
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveWorldDescriptionAsync_SetsCreationTime_WhenZero()
    {
        var worldsRoot = CreateWorldsRoot("0.10.0");

        var world = new WorldDescription { WorldName = "Brand New", CreationTime = 0d };
        Assert.Equal(0d, world.CreationTime);

        await _sut.SaveWorldDescriptionAsync("island-fresh", world);

        // The in-memory object should have been assigned a non-zero CreationTime.
        Assert.True(world.CreationTime > 0d);

        // Also verify from disk.
        var result = await _sut.LoadWorldDescriptionAsync("island-fresh");
        Assert.NotNull(result);
        Assert.True(result!.CreationTime > 0d);
    }

    // ---------------------------------------------------------------
    // 13. DeleteWorldAsync — removes the world directory
    // ---------------------------------------------------------------
    [Fact]
    public async Task DeleteWorldAsync_RemovesDirectory()
    {
        const string islandId = "island-doomed";
        var worldDir = CreateWorldDirectory("0.10.0", islandId);
        await File.WriteAllTextAsync(Path.Combine(worldDir, "WorldDescription.json"), "{}");

        Assert.True(Directory.Exists(worldDir));

        await _sut.DeleteWorldAsync(islandId);

        Assert.False(Directory.Exists(worldDir));
    }

    // ---------------------------------------------------------------
    // 14. DeleteWorldAsync — no-op when directory is missing
    // ---------------------------------------------------------------
    [Fact]
    public async Task DeleteWorldAsync_NoOp_WhenDirMissing()
    {
        Directory.CreateDirectory(_installDir);

        // Should not throw even though the island directory never existed.
        await _sut.DeleteWorldAsync("island-phantom");
    }

    // ---------------------------------------------------------------
    // 15. GetServerDescriptionPath — null when no install dir
    // ---------------------------------------------------------------
    [Fact]
    public void GetServerDescriptionPath_ReturnsNull_WhenNoInstallDir()
    {
        var emptySettings = new FakeAppSettingsService(string.Empty);
        var sut = new ServerConfigService(NullLogger<ServerConfigService>.Instance, emptySettings);

        var result = sut.GetServerDescriptionPath();

        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // Bonus: SaveWorldDescriptionAsync does not overwrite CreationTime when already set
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveWorldDescriptionAsync_PreservesCreationTime_WhenAlreadySet()
    {
        var worldsRoot = CreateWorldsRoot("0.10.0");

        const double originalTime = 638500000000000000d;
        var world = new WorldDescription { WorldName = "Old World", CreationTime = originalTime };
        await _sut.SaveWorldDescriptionAsync("island-old", world);

        Assert.Equal(originalTime, world.CreationTime);

        var result = await _sut.LoadWorldDescriptionAsync("island-old");
        Assert.NotNull(result);
        Assert.Equal(originalTime, result!.CreationTime);
    }

    // ---------------------------------------------------------------
    // Bonus: GetServerDescriptionPath returns correct path when configured
    // ---------------------------------------------------------------
    [Fact]
    public void GetServerDescriptionPath_ReturnsPath_WhenInstallDirSet()
    {
        Directory.CreateDirectory(_installDir);

        var result = _sut.GetServerDescriptionPath();

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(_installDir, "R5", "ServerDescription.json"), result);
    }

    // ---------------------------------------------------------------
    // Bonus: ListWorldIds ignores files, only returns directories
    // ---------------------------------------------------------------
    [Fact]
    public void ListWorldIds_IgnoresFiles_InWorldsDir()
    {
        var worldsRoot = CreateWorldsRoot("0.10.0");
        Directory.CreateDirectory(Path.Combine(worldsRoot, "island-real"));
        File.WriteAllText(Path.Combine(worldsRoot, "readme.txt"), "not a world");

        var result = _sut.ListWorldIds().ToList();

        Assert.Single(result);
        Assert.Contains("island-real", result);
    }

    // ---------------------------------------------------------------
    // Bonus: SaveServerDescriptionToAsync writes to explicit path (wizard scenario)
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveServerDescriptionToAsync_WritesToExplicitPath()
    {
        var otherDir = Path.Combine(_tempRoot, "OtherServer");
        Directory.CreateDirectory(otherDir);

        var sut = new ServerConfigService(NullLogger<ServerConfigService>.Instance, _settings);
        var desc = new ServerDescription { ServerName = "Wizard Server" };

        await sut.SaveServerDescriptionToAsync(otherDir, desc);

        var path = Path.Combine(otherDir, "R5", "ServerDescription.json");
        Assert.True(File.Exists(path));

        var loaded = await sut.LoadServerDescriptionFromAsync(otherDir);
        Assert.NotNull(loaded);
        Assert.Equal("Wizard Server", loaded!.ServerName);
    }

    // ---------------------------------------------------------------
    // Bonus: LoadServerDescriptionFromAsync throws on null installDir
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadServerDescriptionFromAsync_Throws_WhenInstallDirNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.LoadServerDescriptionFromAsync(null!));
    }

    // ---------------------------------------------------------------
    // Bonus: LoadWorldDescriptionAsync returns null for empty islandId
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadWorldDescriptionAsync_ReturnsNull_ForEmptyIslandId()
    {
        var result = await _sut.LoadWorldDescriptionAsync("");
        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // Bonus: SaveWorldDescriptionAsync throws on null world
    // ---------------------------------------------------------------
    [Fact]
    public async Task SaveWorldDescriptionAsync_Throws_WhenWorldNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.SaveWorldDescriptionAsync("island-x", null!));
    }

    // ---------------------------------------------------------------
    // Bonus: DeleteWorldAsync throws on empty islandId
    // ---------------------------------------------------------------
    [Fact]
    public async Task DeleteWorldAsync_Throws_WhenIslandIdEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.DeleteWorldAsync(""));
    }

    // ---------------------------------------------------------------
    // Bonus: LoadWorldDescriptionAsync returns null when file missing
    // ---------------------------------------------------------------
    [Fact]
    public async Task LoadWorldDescriptionAsync_ReturnsNull_WhenFileMissing()
    {
        const string islandId = "island-empty";
        CreateWorldDirectory("0.10.0", islandId);

        var result = await _sut.LoadWorldDescriptionAsync(islandId);
        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // Bonus: ListWorldIds picks newest version directory when multiple exist
    // ---------------------------------------------------------------
    [Fact]
    public void ListWorldIds_PicksNewestVersion_WhenMultipleExist()
    {
        var rocksDbDir = Path.Combine(_installDir, "R5", "Saved", "SaveProfiles", "Default", "RocksDB");
        Directory.CreateDirectory(rocksDbDir);

        // Create two version directories with different timestamps.
        var oldVersionDir = Path.Combine(rocksDbDir, "0.9.0", "Worlds");
        Directory.CreateDirectory(oldVersionDir);
        Directory.CreateDirectory(Path.Combine(oldVersionDir, "island-old"));

        // Small delay to ensure newer timestamp.
        Thread.Sleep(50);

        var newVersionDir = Path.Combine(rocksDbDir, "0.10.0", "Worlds");
        Directory.CreateDirectory(newVersionDir);
        Directory.CreateDirectory(Path.Combine(newVersionDir, "island-new"));

        var result = _sut.ListWorldIds().ToList();

        // Should pick the newest version directory (0.10.0).
        Assert.Single(result);
        Assert.Contains("island-new", result);
    }

    // ===============================================================
    // Helpers
    // ===============================================================

    /// <summary>
    /// Writes raw JSON into the R5/ServerDescription.json location.
    /// </summary>
    private async Task WriteServerDescriptionFile(string json)
    {
        var dir = Path.Combine(_installDir, "R5");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "ServerDescription.json"), json);
    }

    /// <summary>
    /// Creates the full RocksDB/{version}/Worlds directory under the install dir.
    /// </summary>
    private string CreateWorldsRoot(string version)
    {
        var worldsRoot = Path.Combine(_installDir, "R5", "Saved", "SaveProfiles", "Default",
            "RocksDB", version, "Worlds");
        Directory.CreateDirectory(worldsRoot);
        return worldsRoot;
    }

    /// <summary>
    /// Creates a world directory under the given game version.
    /// </summary>
    private string CreateWorldDirectory(string version, string islandId)
    {
        var worldsRoot = CreateWorldsRoot(version);
        var worldDir = Path.Combine(worldsRoot, islandId);
        Directory.CreateDirectory(worldDir);
        return worldDir;
    }

    // ===============================================================
    // Inline fake IAppSettingsService
    // ===============================================================

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        private readonly string _activeServerDir;

        public FakeAppSettingsService(string activeServerDir)
        {
            _activeServerDir = activeServerDir;
            Current = new AppSettings();
        }

        public AppSettings Current { get; }
        public string ActiveServerDir => _activeServerDir;
        public event Action<AppSettings>? Changed { add { } remove { } }

        public Task SelectServerAsync(string serverId) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
        {
            mutate(Current);
            return Task.CompletedTask;
        }
    }
}
