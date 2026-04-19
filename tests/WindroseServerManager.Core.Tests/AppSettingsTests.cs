using WindroseServerManager.Core.Models;
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
}
