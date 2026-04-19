using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase9;

public class OptInMigrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public OptInMigrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WSM-Phase9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }

    private void WriteSettings(AppSettings s)
    {
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        File.WriteAllText(_settingsPath, json);
    }

    private AppSettingsService NewService() =>
        new(NullLogger<AppSettingsService>.Instance, _settingsPath);

    [Fact]
    public async Task LoadAsync_SeedsNeverAsked_ForKnownServers_WithoutOptInEntry()
    {
        var s = new AppSettings();
        s.WindrosePlusActiveByServer["C:\\servers\\s1"] = true;
        s.WindrosePlusActiveByServer["C:\\servers\\s2"] = false;
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();

        Assert.Equal(OptInState.NeverAsked, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1"]);
        Assert.Equal(OptInState.NeverAsked, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s2"]);
    }

    [Fact]
    public async Task LoadAsync_Idempotent_DoesNotOverwriteOptedOut()
    {
        var s = new AppSettings();
        s.WindrosePlusActiveByServer["C:\\servers\\s1"] = false;
        s.WindrosePlusOptInStateByServer["C:\\servers\\s1"] = OptInState.OptedOut;
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();
        Assert.Equal(OptInState.OptedOut, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1"]);

        // Persist + reload → still OptedOut.
        await svc.SaveAsync();
        var svc2 = NewService();
        await svc2.LoadAsync();
        Assert.Equal(OptInState.OptedOut, svc2.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1"]);
    }

    [Fact]
    public async Task LoadAsync_Idempotent_DoesNotOverwriteOptedIn()
    {
        var s = new AppSettings();
        s.WindrosePlusActiveByServer["C:\\servers\\s1"] = true;
        s.WindrosePlusOptInStateByServer["C:\\servers\\s1"] = OptInState.OptedIn;
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();
        Assert.Equal(OptInState.OptedIn, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1"]);
    }

    [Fact]
    public async Task LoadAsync_NoKnownServers_LeavesOptInDictEmpty()
    {
        var s = new AppSettings();
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();
        Assert.Empty(svc.Current.WindrosePlusOptInStateByServer);
    }

    [Fact]
    public async Task LoadAsync_MigrationCompletesSynchronously_BeforeReturn()
    {
        var s = new AppSettings();
        s.WindrosePlusActiveByServer["C:\\servers\\s1"] = true;
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();

        // Immediately after LoadAsync completes, migrated state MUST already be visible.
        Assert.True(svc.Current.WindrosePlusOptInStateByServer.ContainsKey("C:\\servers\\s1"));
        Assert.Equal(OptInState.NeverAsked, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1"]);
    }

    [Fact]
    public async Task LoadAsync_OrphanOptInKey_IsNotRemoved()
    {
        var s = new AppSettings();
        // "s1" exists only in the OptInState dict — server was deleted but decision persists.
        s.WindrosePlusOptInStateByServer["C:\\servers\\s1-deleted"] = OptInState.OptedIn;
        WriteSettings(s);

        var svc = NewService();
        await svc.LoadAsync();

        Assert.True(svc.Current.WindrosePlusOptInStateByServer.ContainsKey("C:\\servers\\s1-deleted"));
        Assert.Equal(OptInState.OptedIn, svc.Current.WindrosePlusOptInStateByServer["C:\\servers\\s1-deleted"]);
    }

    [Fact]
    public async Task LoadAsync_NoFile_StillInitializesEmptyMigration()
    {
        // No settings file written — fresh install.
        var svc = NewService();
        await svc.LoadAsync();
        Assert.NotNull(svc.Current.WindrosePlusOptInStateByServer);
        Assert.Empty(svc.Current.WindrosePlusOptInStateByServer);
    }
}
