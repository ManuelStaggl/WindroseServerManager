namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusEvent(
    string Type,
    string? SteamId,
    string Name,
    DateTime Timestamp);
