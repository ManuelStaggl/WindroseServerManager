using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IServerEventLog
{
    /// <summary>Wird nach dem Append gefeuert. Auf UI-Thread marshallen!</summary>
    event Action<ServerEvent>? Appended;

    Task AppendAsync(ServerEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<ServerEvent>> ReadRecentAsync(int maxCount = 100, CancellationToken ct = default);
}
