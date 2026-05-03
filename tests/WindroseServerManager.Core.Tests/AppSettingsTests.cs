using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultValues_AreSafe()
    {
        var s = new AppSettings();

        Assert.True(s.LogEnabled);
        Assert.Equal(5, s.GracefulShutdownSeconds);
        Assert.Equal("4129620", s.SteamAppId);
        Assert.Equal("auto", s.Language);
        Assert.False(s.AutoRestartOnCrash);
        Assert.False(s.AutoBackupEnabled);
        Assert.Equal(20, s.MaxBackupsToKeep);
        Assert.Equal("04:00", s.DailyRestartTime);
    }

    [Fact]
    public void WindrosePlusActive_RoundTrip()
    {
        var s = new AppSettings();
        s.WindrosePlusActiveByServer["C:\\servers\\s1"] = true;
        s.WindrosePlusVersionByServer["C:\\servers\\s1"] = "v1.0.6";
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.True(restored.WindrosePlusActiveByServer["C:\\servers\\s1"]);
        Assert.Equal("v1.0.6", restored.WindrosePlusVersionByServer["C:\\servers\\s1"]);
    }

    [Fact]
    public async Task ConcurrentUpdates_SaveValidSettingsWithoutCollectionMutation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"wsm-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(tempDir, "settings.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = new AppSettingsService(NullLogger<AppSettingsService>.Instance, settingsPath);
            await service.SaveAsync();

            var tasks = Enumerable.Range(0, 40)
                .Select(i => service.UpdateAsync(s =>
                {
                    var dir = $"C:\\servers\\s{i}";
                    s.Servers.Add(new ServerEntry
                    {
                        Id = i.ToString("D2"),
                        Name = $"Server {i}",
                        InstallDir = dir,
                    });
                    s.WindrosePlusActiveByServer[dir] = i % 2 == 0;
                    s.RestartDays.Add((DayOfWeek)(i % 7));

                    Thread.Sleep(2);

                    s.WindrosePlusVersionByServer[dir] = $"v{i}";
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var json = await File.ReadAllTextAsync(settingsPath);
            var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            });

            Assert.NotNull(restored);
            Assert.Equal(40, restored.Servers.Count);
            Assert.Equal(40, restored.WindrosePlusActiveByServer.Count);
            Assert.Equal(40, restored.WindrosePlusVersionByServer.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
