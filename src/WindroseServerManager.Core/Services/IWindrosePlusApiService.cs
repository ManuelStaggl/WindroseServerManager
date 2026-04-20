using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IWindrosePlusApiService
{
    Task<WindrosePlusStatusResult?> GetStatusAsync(string serverDir, CancellationToken ct = default);
    Task<WindrosePlusQueryResult?>  QueryAsync(string serverDir, CancellationToken ct = default);
    Task<string?>                   RconAsync(string serverDir, string command, CancellationToken ct = default);
    WindrosePlusConfig?             ReadConfig(string serverDir);
    Task                            WriteConfigAsync(string serverDir, WindrosePlusConfig config, CancellationToken ct = default);
    string BuildKickCommand(string identifier);
    string BuildBanCommand(string identifier, int? minutes);
    string BuildBroadcastCommand(string message);
}
