using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface INexusClient
{
    /// <summary>Liefert Metadaten zu einer Mod. Null wenn API-Key fehlt oder Mod nicht (mehr) existiert.</summary>
    Task<NexusModInfo?> GetModAsync(int modId, CancellationToken ct = default);

    /// <summary>
    /// True wenn ein API-Key hinterlegt ist. Ohne Key sind alle Calls ausgeschaltet.
    /// </summary>
    bool IsConfigured { get; }
}
