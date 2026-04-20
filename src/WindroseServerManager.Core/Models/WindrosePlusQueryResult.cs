namespace WindroseServerManager.Core.Models;

public sealed record WindrosePlusQueryResult(
    IReadOnlyList<WindrosePlusPlayer> Players);
