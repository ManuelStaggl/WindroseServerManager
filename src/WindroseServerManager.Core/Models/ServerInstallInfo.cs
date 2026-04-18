namespace WindroseServerManager.Core.Models;

public sealed record ServerInstallInfo(
    bool IsInstalled,
    string InstallDir,
    string? BuildId,
    long SizeBytes,
    DateTime? LastUpdatedUtc)
{
    public static ServerInstallInfo NotInstalled(string installDir) =>
        new(false, installDir, null, 0, null);
}
