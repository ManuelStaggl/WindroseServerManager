namespace WindroseServerManager.Core.Models;

public sealed record ServerInstallInfo(
    bool IsInstalled,
    string InstallDir,
    string? BuildId,
    long SizeBytes,
    DateTime? LastUpdatedUtc,
    bool WindrosePlusActive = false,
    string? WindrosePlusVersionTag = null,
    string? WindrosePlusRconPassword = null,
    int WindrosePlusDashboardPort = 0,
    string? WindrosePlusAdminSteamId = null,
    OptInState WindrosePlusOptInState = OptInState.NeverAsked)
{
    public static ServerInstallInfo NotInstalled(string installDir) =>
        new(false, installDir, null, 0, null);
}
