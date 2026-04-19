using System.Text.Json;
using WindroseServerManager.Core.Models;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase9;

public class AppSettingsPhase9Tests
{
    [Fact]
    public void OptInState_Enum_HasExpectedMembers()
    {
        Assert.Equal(0, (int)OptInState.NeverAsked);
        Assert.True(Enum.IsDefined(typeof(OptInState), OptInState.OptedIn));
        Assert.True(Enum.IsDefined(typeof(OptInState), OptInState.OptedOut));
    }

    [Fact]
    public void AppSettings_Phase9Dicts_Exist_And_DefaultEmpty()
    {
        var s = new AppSettings();
        Assert.NotNull(s.WindrosePlusRconPasswordByServer);
        Assert.NotNull(s.WindrosePlusDashboardPortByServer);
        Assert.NotNull(s.WindrosePlusAdminSteamIdByServer);
        Assert.NotNull(s.WindrosePlusOptInStateByServer);
        Assert.Empty(s.WindrosePlusRconPasswordByServer);
        Assert.Empty(s.WindrosePlusDashboardPortByServer);
        Assert.Empty(s.WindrosePlusAdminSteamIdByServer);
        Assert.Empty(s.WindrosePlusOptInStateByServer);
    }

    [Fact]
    public void AppSettings_Phase9Dicts_RoundTrip_PreservesValues_AndEnumAsString()
    {
        const string key = "C:\\servers\\s1";
        var s = new AppSettings();
        s.WindrosePlusRconPasswordByServer[key] = "AbCdEfGhIjKlMnOpQrStUvWx";
        s.WindrosePlusDashboardPortByServer[key] = 18081;
        s.WindrosePlusAdminSteamIdByServer[key] = "76561198012345678";
        s.WindrosePlusOptInStateByServer[key] = OptInState.OptedIn;

        var json = JsonSerializer.Serialize(s);
        Assert.Contains("\"OptedIn\"", json); // human-readable enum

        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.Equal("AbCdEfGhIjKlMnOpQrStUvWx", restored.WindrosePlusRconPasswordByServer[key]);
        Assert.Equal(18081, restored.WindrosePlusDashboardPortByServer[key]);
        Assert.Equal("76561198012345678", restored.WindrosePlusAdminSteamIdByServer[key]);
        Assert.Equal(OptInState.OptedIn, restored.WindrosePlusOptInStateByServer[key]);
    }

    [Fact]
    public void ServerInstallInfo_Phase9_Defaults_AreCorrect()
    {
        var info = ServerInstallInfo.NotInstalled("C:\\servers\\s1");
        Assert.Null(info.WindrosePlusRconPassword);
        Assert.Equal(0, info.WindrosePlusDashboardPort);
        Assert.Null(info.WindrosePlusAdminSteamId);
        Assert.Equal(OptInState.NeverAsked, info.WindrosePlusOptInState);
    }

    [Fact]
    public void ServerInstallInfo_Phase9_PositionalParameters_InOrder()
    {
        var info = new ServerInstallInfo(
            IsInstalled: true,
            InstallDir: "C:\\servers\\s1",
            BuildId: "123",
            SizeBytes: 42,
            LastUpdatedUtc: null,
            WindrosePlusActive: true,
            WindrosePlusVersionTag: "v1.0.6",
            WindrosePlusRconPassword: "secret",
            WindrosePlusDashboardPort: 18080,
            WindrosePlusAdminSteamId: "76561198012345678",
            WindrosePlusOptInState: OptInState.OptedOut);

        Assert.Equal("secret", info.WindrosePlusRconPassword);
        Assert.Equal(18080, info.WindrosePlusDashboardPort);
        Assert.Equal("76561198012345678", info.WindrosePlusAdminSteamId);
        Assert.Equal(OptInState.OptedOut, info.WindrosePlusOptInState);
    }
}
