using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class ModServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _serverInstall;
    private readonly string _modsDir;
    private readonly FakeAppSettings _settings;
    private readonly FakeProcessService _process;
    private readonly ModService _sut;

    public ModServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "wrsm-modtests-" + Guid.NewGuid().ToString("N"));
        _serverInstall = Path.Combine(_tempRoot, "server");
        _modsDir = Path.Combine(_serverInstall, "R5", "Content", "Paks", "~mods");
        Directory.CreateDirectory(_serverInstall);

        _settings = new FakeAppSettings { Current = { ServerInstallDir = _serverInstall } };
        _process = new FakeProcessService();
        _sut = new ModService(NullLogger<ModService>.Instance, _settings, _process);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void GetModsDir_CreatesDirectory()
    {
        var dir = _sut.GetModsDir();
        Assert.NotNull(dir);
        Assert.True(Directory.Exists(dir));
        Assert.Equal(_modsDir, dir);
    }

    [Fact]
    public void GetModsDir_Null_WhenInstallDirEmpty()
    {
        _settings.Current.ServerInstallDir = "";
        Assert.Null(_sut.GetModsDir());
    }

    [Fact]
    public void ValidateReady_BlocksWhenServerRunning()
    {
        _process.Status = ServerStatus.Running;
        Assert.NotNull(_sut.ValidateReady());
    }

    [Fact]
    public void ValidateReady_OkWhenStopped()
    {
        _process.Status = ServerStatus.Stopped;
        Assert.Null(_sut.ValidateReady());
    }

    [Fact]
    public async Task InstallFromPak_Happy()
    {
        var pak = CreateFakePak("CoolMod.pak");
        var result = await _sut.InstallFromArchiveAsync(pak);

        Assert.Single(result);
        Assert.Equal("CoolMod.pak", result[0].FileName);
        Assert.True(result[0].IsEnabled);
        Assert.True(File.Exists(Path.Combine(_modsDir, "CoolMod.pak")));
    }

    [Fact]
    public async Task InstallFromPak_CopiesCompanions()
    {
        var pakSrc = CreateFakePak("ModWithCompanions.pak");
        var srcDir = Path.GetDirectoryName(pakSrc)!;
        File.WriteAllBytes(Path.Combine(srcDir, "ModWithCompanions.ucas"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(srcDir, "ModWithCompanions.utoc"), [4, 5, 6]);

        var result = await _sut.InstallFromArchiveAsync(pakSrc);

        Assert.Equal(2, result[0].CompanionFiles.Count);
        Assert.True(File.Exists(Path.Combine(_modsDir, "ModWithCompanions.ucas")));
        Assert.True(File.Exists(Path.Combine(_modsDir, "ModWithCompanions.utoc")));
    }

    [Fact]
    public async Task InstallFromZip_ExtractsPakAndCompanions()
    {
        var zipPath = Path.Combine(_tempRoot, "bundle.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddEntry(zip, "ZipMod.pak", [0x50, 0x41, 0x4b]);
            AddEntry(zip, "ZipMod.ucas", [1]);
            AddEntry(zip, "ZipMod.utoc", [2]);
            AddEntry(zip, "README.txt", [9, 9]); // should be ignored
        }

        var result = await _sut.InstallFromArchiveAsync(zipPath);
        Assert.Single(result);
        Assert.Equal("ZipMod.pak", result[0].FileName);
        Assert.True(File.Exists(Path.Combine(_modsDir, "ZipMod.pak")));
        Assert.True(File.Exists(Path.Combine(_modsDir, "ZipMod.ucas")));
        Assert.True(File.Exists(Path.Combine(_modsDir, "ZipMod.utoc")));
        Assert.False(File.Exists(Path.Combine(_modsDir, "README.txt")));
    }

    [Fact]
    public async Task InstallFromArchive_Fails_WhenServerRunning()
    {
        _process.Status = ServerStatus.Running;
        var pak = CreateFakePak("Blocked.pak");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.InstallFromArchiveAsync(pak));
    }

    [Fact]
    public async Task SetEnabled_TogglesFilename_Idempotent()
    {
        var pak = CreateFakePak("Toggler.pak");
        await _sut.InstallFromArchiveAsync(pak);

        _sut.SetEnabled("Toggler.pak", enabled: false);
        Assert.False(File.Exists(Path.Combine(_modsDir, "Toggler.pak")));
        Assert.True(File.Exists(Path.Combine(_modsDir, "Toggler.pak.disabled")));

        // Idempotent — keine Exception wenn nochmal disabled
        _sut.SetEnabled("Toggler.pak.disabled", enabled: false);
        Assert.True(File.Exists(Path.Combine(_modsDir, "Toggler.pak.disabled")));

        _sut.SetEnabled("Toggler.pak.disabled", enabled: true);
        Assert.True(File.Exists(Path.Combine(_modsDir, "Toggler.pak")));
        Assert.False(File.Exists(Path.Combine(_modsDir, "Toggler.pak.disabled")));
    }

    [Fact]
    public async Task Uninstall_RemovesPakAndCompanions()
    {
        var pak = CreateFakePak("DoomedMod.pak");
        var srcDir = Path.GetDirectoryName(pak)!;
        File.WriteAllBytes(Path.Combine(srcDir, "DoomedMod.ucas"), [1]);
        await _sut.InstallFromArchiveAsync(pak);

        _sut.UninstallMod("DoomedMod.pak");

        Assert.False(File.Exists(Path.Combine(_modsDir, "DoomedMod.pak")));
        Assert.False(File.Exists(Path.Combine(_modsDir, "DoomedMod.ucas")));
    }

    [Fact]
    public async Task ListMods_IncludesDisabled_FlagCorrect()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("A.pak"));
        await _sut.InstallFromArchiveAsync(CreateFakePak("B.pak"));
        _sut.SetEnabled("B.pak", enabled: false);

        var mods = _sut.ListMods().ToList();
        Assert.Equal(2, mods.Count);
        Assert.Contains(mods, m => m.FileName == "A.pak" && m.IsEnabled);
        Assert.Contains(mods, m => m.FileName == "B.pak.disabled" && !m.IsEnabled);
    }

    [Fact]
    public async Task ExportClientBundle_OnlyActiveMods()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("Active1.pak"));
        await _sut.InstallFromArchiveAsync(CreateFakePak("Active2.pak"));
        await _sut.InstallFromArchiveAsync(CreateFakePak("Inactive.pak"));
        _sut.SetEnabled("Inactive.pak", enabled: false);

        var zipPath = Path.Combine(_tempRoot, "client.zip");
        await _sut.ExportClientBundleAsync(zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.Name).ToList();
        Assert.Contains("Active1.pak", names);
        Assert.Contains("Active2.pak", names);
        Assert.DoesNotContain("Inactive.pak", names);
        Assert.DoesNotContain("Inactive.pak.disabled", names);
    }

    [Fact]
    public async Task ExportClientBundle_Throws_WhenNoActiveMods()
    {
        var zipPath = Path.Combine(_tempRoot, "empty.zip");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExportClientBundleAsync(zipPath));
    }

    [Fact]
    public async Task SetMeta_WritesSideCar_GetMeta_Reads()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("Linked.pak"));
        var meta = new ModMeta(42, DateTime.UtcNow);
        _sut.SetMeta("Linked.pak", meta);

        var read = _sut.GetMeta("Linked.pak");
        Assert.NotNull(read);
        Assert.Equal(42, read!.NexusModId);
    }

    [Fact]
    public async Task ListMods_PopulatesMeta_WhenSideCarExists()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("Linked.pak"));
        _sut.SetMeta("Linked.pak", new ModMeta(7, DateTime.UtcNow));

        var mod = _sut.ListMods().Single();
        Assert.NotNull(mod.NexusMeta);
        Assert.Equal(7, mod.NexusMeta!.NexusModId);
        // Display-Name kommt immer aus dem Dateinamen (kein API-Metadata mehr)
        Assert.Equal("Linked", mod.DisplayName);
    }

    [Fact]
    public async Task SetEnabled_PreservesMeta()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("Toggle.pak"));
        _sut.SetMeta("Toggle.pak", new ModMeta(99, DateTime.UtcNow));

        _sut.SetEnabled("Toggle.pak", enabled: false);

        var meta = _sut.GetMeta("Toggle.pak.disabled");
        Assert.NotNull(meta);
        Assert.Equal(99, meta!.NexusModId);
    }

    [Fact]
    public async Task Uninstall_RemovesMeta()
    {
        await _sut.InstallFromArchiveAsync(CreateFakePak("Doomed.pak"));
        _sut.SetMeta("Doomed.pak", new ModMeta(1, DateTime.UtcNow));

        _sut.UninstallMod("Doomed.pak");

        Assert.Null(_sut.GetMeta("Doomed.pak"));
        Assert.False(File.Exists(Path.Combine(_modsDir, "Doomed.pak.meta.json")));
    }

    [Fact]
    public void ClearMeta_RemovesSideCar()
    {
        _sut.InstallFromArchiveAsync(CreateFakePak("A.pak")).GetAwaiter().GetResult();
        _sut.SetMeta("A.pak", new ModMeta(1, DateTime.UtcNow));

        _sut.ClearMeta("A.pak");
        Assert.Null(_sut.GetMeta("A.pak"));
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private string CreateFakePak(string name)
    {
        var src = Path.Combine(_tempRoot, "src");
        Directory.CreateDirectory(src);
        var path = Path.Combine(src, name);
        File.WriteAllBytes(path, [0x50, 0x41, 0x4b, 0x30]); // "PAK0" dummy
        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private sealed class FakeAppSettings : IAppSettingsService
    {
        public AppSettings Current { get; } = new();
        public event Action<AppSettings>? Changed;
        public string ActiveServerDir => Current.ServerInstallDir;
        public Task SelectServerAsync(string id) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
        {
            mutate(Current);
            Changed?.Invoke(Current);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessService : IServerProcessService
    {
        public ServerStatus Status { get; set; } = ServerStatus.Stopped;
        public DateTime? StartedAtUtc => null;
        public int? ProcessId => null;
        public int? LastExitCode => null;
        public IReadOnlyList<ServerLogLine> RecentLog => Array.Empty<ServerLogLine>();
        public event Action<ServerStatus>? StatusChanged;
        public event Action<ServerLogLine>? LogAppended;
        public Task<bool> StartAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task KillAsync(CancellationToken ct = default) => Task.CompletedTask;
        public string? ValidateCanStart() => null;
        public bool TryAttachToExistingProcess() => false;
    }
}
