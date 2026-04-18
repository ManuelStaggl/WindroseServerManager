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
        Assert.Equal("de", s.Language);
        Assert.False(s.AutoRestartOnCrash);
        Assert.False(s.AutoBackupEnabled);
        Assert.Equal(20, s.MaxBackupsToKeep);
        Assert.Equal("04:00", s.DailyRestartTime);
    }
}
