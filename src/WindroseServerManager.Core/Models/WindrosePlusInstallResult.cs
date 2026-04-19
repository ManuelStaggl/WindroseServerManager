namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusInstallResult(
    string Tag,
    string ServerInstallDir,
    DateTime InstalledUtc,
    string ArchiveSha256,
    string? Ue4ssTag);
