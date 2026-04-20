namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusStatusResult(
    IReadOnlyList<WindrosePlusPlayer> Players,
    string? ServerName,
    string? WindrosePlusVersion);
