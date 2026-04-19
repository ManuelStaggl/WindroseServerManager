namespace WindroseServerManager.Core.Models;

/// <summary>Parsed GitHub Releases API response for WindrosePlus or UE4SS.</summary>
public sealed record WindrosePlusRelease(
    string Tag,
    string AssetName,
    string DownloadUrl,
    long SizeBytes,
    string? DigestSha256);
