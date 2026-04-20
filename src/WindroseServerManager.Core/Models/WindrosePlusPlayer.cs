namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusPlayer(
    string SteamId,
    string Name,
    bool Alive,
    int SessionSeconds,
    double? WorldX = null,
    double? WorldY = null,
    string? ShipInfo = null);
