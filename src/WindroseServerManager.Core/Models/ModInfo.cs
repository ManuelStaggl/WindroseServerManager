namespace WindroseServerManager.Core.Models;

public sealed record ModInfo(
    string FileName,
    string FullPath,
    string DisplayName,
    long SizeBytes,
    DateTime InstalledUtc,
    bool IsEnabled,
    IReadOnlyList<string> CompanionFiles,
    ModMeta? NexusMeta = null);
