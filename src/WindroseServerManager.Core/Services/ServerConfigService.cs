using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class ServerConfigService : IServerConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,          // Windrose uses PascalCase keys
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private const string ServerDescriptionFileName = "ServerDescription.json";
    private const string WorldDescriptionFileName = "WorldDescription.json";

    // Offiziell: {InstallDir}\R5\ServerDescription.json
    private static readonly string ServerDescriptionRelativeDir = "R5";

    // Offizielle Struktur laut Windrose Community-Guide:
    // {ServerInstallDir}\R5\Saved\SaveProfiles\Default\RocksDB\{GameVersion}\Worlds\{IslandId}\WorldDescription.json
    private static readonly string RocksDbRelativeDir =
        Path.Combine("R5", "Saved", "SaveProfiles", "Default", "RocksDB");

    private readonly ILogger<ServerConfigService> _logger;
    private readonly IAppSettingsService _settings;

    // Envelope-Cache damit wir Version/DeploymentId/unknown-fields beim Schreiben durchreichen.
    private ServerDescriptionFile? _lastServerEnvelope;
    private readonly Dictionary<string, WorldDescriptionFile> _lastWorldEnvelopes = new(StringComparer.OrdinalIgnoreCase);

    public ServerConfigService(ILogger<ServerConfigService> logger, IAppSettingsService settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public string? GetConfigRoot()
    {
        var dir = _settings.ActiveServerDir;
        return string.IsNullOrWhiteSpace(dir) ? null : dir;
    }

    public string? GetServerDescriptionPath()
    {
        var root = GetConfigRoot();
        return root is null ? null : Path.Combine(root, ServerDescriptionRelativeDir, ServerDescriptionFileName);
    }

    public string? GetWorldsRoot() => ResolveWorldsRoot();

    public string? GetWorldDir(string islandId)
    {
        if (string.IsNullOrWhiteSpace(islandId)) return null;
        var worldsRoot = ResolveWorldsRoot();
        return worldsRoot is null ? null : Path.Combine(worldsRoot, islandId);
    }

    // Fallback wenn weder existierender Version-Ordner noch DeploymentId verfügbar sind.
    // Server erkennt den Pfad beim ersten Start oder migriert die Welt in seinen aktuellen Version-Ordner.
    private const string DefaultGameVersion = "0.10.0";

    private string? ResolveWorldsRoot() => ResolveOrCreateWorldsRoot(createIfMissing: false);

    /// <summary>
    /// Sucht den aktuellsten GameVersion-Unterordner unter {InstallDir}\R5\Saved\SaveProfiles\Default\RocksDB\.
    /// Wenn keiner existiert und <paramref name="createIfMissing"/> true ist, wird ein Ordner
    /// mit einer aus DeploymentId abgeleiteten (oder Default-)GameVersion angelegt.
    /// </summary>
    private string? ResolveOrCreateWorldsRoot(bool createIfMissing)
    {
        var root = GetConfigRoot();
        if (root is null) return null;

        var rocksDbDir = Path.Combine(root, RocksDbRelativeDir);

        if (Directory.Exists(rocksDbDir))
        {
            var versionDirs = Directory.EnumerateDirectories(rocksDbDir)
                .Select(p => new DirectoryInfo(p))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .ToList();

            if (versionDirs.Count > 0)
            {
                if (versionDirs.Count > 1)
                    _logger.LogDebug("Mehrere GameVersion-Ordner gefunden, nehme neuesten: {Version}", versionDirs[0].Name);
                return Path.Combine(versionDirs[0].FullName, "Worlds");
            }
        }

        if (!createIfMissing) return null;

        // Keine Version-Ordner vorhanden → GameVersion aus DeploymentId ableiten oder Default.
        var version = InferGameVersion() ?? DefaultGameVersion;
        var target = Path.Combine(rocksDbDir, version, "Worlds");
        try
        {
            Directory.CreateDirectory(target);
            _logger.LogInformation("Worlds-Ordner neu angelegt ({Path}). Server wird ihn beim Start übernehmen.", target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Konnte Worlds-Ordner nicht anlegen: {Path}", target);
            return null;
        }
        return target;
    }

    /// <summary>
    /// Zieht z. B. "0.10.0" aus DeploymentId "0.10.0.0.251-master-9f800c33".
    /// </summary>
    private string? InferGameVersion()
    {
        // 1) gecachter Envelope (falls im selben App-Lauf schon geladen)
        var deploymentId = _lastServerEnvelope?.DeploymentId;

        // 2) Versuch: direkt von Disk lesen, ohne Async/Throw
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            var path = GetServerDescriptionPath();
            if (path is not null && File.Exists(path))
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    var env = JsonSerializer.Deserialize<ServerDescriptionFile>(fs, JsonOptions);
                    deploymentId = env?.DeploymentId;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Konnte DeploymentId nicht aus {Path} ableiten", path);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(deploymentId)) return null;

        // "0.10.0.0.251-master-9f800c33" → "0.10.0" (die ersten drei Komponenten vor dem Dash).
        var head = deploymentId.Split('-', 2)[0];
        var parts = head.Split('.');
        if (parts.Length < 3) return null;
        return string.Join('.', parts.Take(3));
    }

    public Task<ServerDescription?> LoadServerDescriptionAsync(CancellationToken ct = default)
    {
        var root = GetConfigRoot();
        if (root is null) return Task.FromResult<ServerDescription?>(null);
        return LoadServerDescriptionCoreAsync(root, updateCache: true, ct);
    }

    public Task<ServerDescription?> LoadServerDescriptionFromAsync(string installDir, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDir);
        return LoadServerDescriptionCoreAsync(installDir, updateCache: false, ct);
    }

    private async Task<ServerDescription?> LoadServerDescriptionCoreAsync(string installDir, bool updateCache, CancellationToken ct)
    {
        var path = Path.Combine(installDir, ServerDescriptionRelativeDir, ServerDescriptionFileName);
        if (!File.Exists(path))
        {
            _logger.LogInformation("ServerDescription not found at {Path}", path);
            return null;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            var envelope = await JsonSerializer.DeserializeAsync<ServerDescriptionFile>(stream, JsonOptions, ct)
                               .ConfigureAwait(false)
                           ?? new ServerDescriptionFile();
            if (updateCache) _lastServerEnvelope = envelope;
            return envelope.Persistent ?? new ServerDescription();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {Path}", path);
            throw;
        }
    }

    public Task SaveServerDescriptionAsync(ServerDescription desc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(desc);
        var root = GetConfigRoot();
        if (root is null) throw new InvalidOperationException("Server-Installationspfad ist nicht gesetzt.");
        return SaveServerDescriptionCoreAsync(root, desc, useCachedEnvelope: true, ct);
    }

    public Task SaveServerDescriptionToAsync(string installDir, ServerDescription desc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDir);
        ArgumentNullException.ThrowIfNull(desc);
        return SaveServerDescriptionCoreAsync(installDir, desc, useCachedEnvelope: false, ct);
    }

    private async Task SaveServerDescriptionCoreAsync(string installDir, ServerDescription desc, bool useCachedEnvelope, CancellationToken ct)
    {
        var dir = Path.Combine(installDir, ServerDescriptionRelativeDir);
        Directory.CreateDirectory(dir);

        // Wenn eine Datei für diesen Ordner existiert, vorhandenes Envelope einlesen um
        // Version/DeploymentId/unknown-keys zu bewahren — auch im Wizard-Kontext.
        ServerDescriptionFile? existingEnvelope = useCachedEnvelope ? _lastServerEnvelope : null;
        if (existingEnvelope is null)
        {
            var existingPath = Path.Combine(dir, ServerDescriptionFileName);
            if (File.Exists(existingPath))
            {
                try
                {
                    await using var stream = File.OpenRead(existingPath);
                    existingEnvelope = await JsonSerializer
                        .DeserializeAsync<ServerDescriptionFile>(stream, JsonOptions, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Konnte bestehendes Envelope unter {Path} nicht lesen, schreibe neu", existingPath);
                }
            }
        }

        var envelope = new ServerDescriptionFile
        {
            Version = existingEnvelope?.Version ?? 1,
            DeploymentId = existingEnvelope?.DeploymentId ?? string.Empty,
            ExtensionData = existingEnvelope?.ExtensionData,
            Persistent = desc,
        };

        var path = Path.Combine(dir, ServerDescriptionFileName);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tmp, path, overwrite: true);

        if (useCachedEnvelope) _lastServerEnvelope = envelope;

        _logger.LogInformation("Saved {Path}", path);
    }

    public IEnumerable<string> ListWorldIds()
    {
        var worldsDir = ResolveWorldsRoot();
        if (worldsDir is null || !Directory.Exists(worldsDir)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(worldsDir))
        {
            // Each world folder is named by its IslandId
            yield return Path.GetFileName(dir);
        }
    }

    public async Task<WorldDescription?> LoadWorldDescriptionAsync(string islandId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(islandId)) return null;
        var worldDir = GetWorldDir(islandId);
        if (worldDir is null) return null;

        var path = Path.Combine(worldDir, WorldDescriptionFileName);
        if (!File.Exists(path))
        {
            _logger.LogInformation("WorldDescription not found at {Path}", path);
            return null;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            var envelope = await JsonSerializer.DeserializeAsync<WorldDescriptionFile>(stream, JsonOptions, ct)
                               .ConfigureAwait(false)
                           ?? new WorldDescriptionFile();
            _lastWorldEnvelopes[islandId] = envelope;
            return envelope.Inner ?? new WorldDescription();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {Path}", path);
            throw;
        }
    }

    public async Task SaveWorldDescriptionAsync(string islandId, WorldDescription world, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islandId);
        ArgumentNullException.ThrowIfNull(world);

        // Welt-Speichern darf die Ordnerstruktur selbst anlegen (Tool-First-Create vor erstem Server-Start).
        var worldsRoot = ResolveOrCreateWorldsRoot(createIfMissing: true);
        if (worldsRoot is null)
        {
            throw new InvalidOperationException(
                "Server-Installationspfad ist nicht gesetzt oder nicht beschreibbar.");
        }
        var worldDir = Path.Combine(worldsRoot, islandId);
        Directory.CreateDirectory(worldDir);

        // Ensure IslandId matches folder name (Windrose requires this)
        world.IslandId = islandId;

        // Default CreationTime für neu erstellte Welten
        if (world.CreationTime == 0d)
        {
            world.CreationTime = (double)DateTime.UtcNow.Ticks;
        }

        _lastWorldEnvelopes.TryGetValue(islandId, out var cachedEnvelope);

        var envelope = new WorldDescriptionFile
        {
            Version = cachedEnvelope?.Version ?? 1,
            ExtensionData = cachedEnvelope?.ExtensionData,
            Inner = world,
        };

        var path = Path.Combine(worldDir, WorldDescriptionFileName);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tmp, path, overwrite: true);

        _lastWorldEnvelopes[islandId] = envelope;

        _logger.LogInformation("Saved {Path}", path);
    }

    public Task DeleteWorldAsync(string islandId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(islandId);
        var worldDir = GetWorldDir(islandId);
        if (worldDir is null || !Directory.Exists(worldDir))
        {
            _logger.LogWarning("Welt-Ordner nicht gefunden: {Dir}", worldDir ?? "(null)");
            return Task.CompletedTask;
        }
        try
        {
            Directory.Delete(worldDir, recursive: true);
            _lastWorldEnvelopes.Remove(islandId);
            _logger.LogInformation("Welt gelöscht: {Dir}", worldDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Welt konnte nicht gelöscht werden: {Dir}", worldDir);
            throw;
        }
        return Task.CompletedTask;
    }
}
