using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IServerConfigService
{
    /// <summary>Returns the server install directory (root), or null if not configured.</summary>
    string? GetConfigRoot();

    /// <summary>Absoluter Pfad zur ServerDescription.json (auch wenn noch nicht erzeugt).</summary>
    string? GetServerDescriptionPath();

    /// <summary>Absoluter Pfad zum Worlds-Verzeichnis (z.B. ...\RocksDB\0.10.0\Worlds\). Null wenn kein GameVersion-Ordner existiert.</summary>
    string? GetWorldsRoot();

    /// <summary>Absoluter Pfad zum Ordner einer Welt.</summary>
    string? GetWorldDir(string islandId);

    Task<ServerDescription?> LoadServerDescriptionAsync(CancellationToken ct = default);
    Task SaveServerDescriptionAsync(ServerDescription desc, CancellationToken ct = default);

    /// <summary>Lists world subdirectories found under the resolved Worlds root.</summary>
    IEnumerable<string> ListWorldIds();

    Task<WorldDescription?> LoadWorldDescriptionAsync(string islandId, CancellationToken ct = default);
    Task SaveWorldDescriptionAsync(string islandId, WorldDescription world, CancellationToken ct = default);

    /// <summary>Recursively deletes the world folder for the given IslandId.</summary>
    Task DeleteWorldAsync(string islandId, CancellationToken ct = default);
}
