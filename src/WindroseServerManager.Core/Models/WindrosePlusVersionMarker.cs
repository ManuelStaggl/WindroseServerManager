namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusVersionMarker(
    string Tag,
    DateTime InstalledUtc,
    string ArchiveSha256,
    string? Ue4ssTag);
