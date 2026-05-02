using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<AppSettingsService> _logger;
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSettings _current = new();

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WindroseServerManager");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");

        // Einmalige Migration vom alten Projektnamen "WindroseCommand".
        if (!File.Exists(_settingsPath))
        {
            var legacyPath = Path.Combine(appData, "WindroseCommand", "settings.json");
            if (File.Exists(legacyPath))
            {
                try
                {
                    File.Copy(legacyPath, _settingsPath);
                    _logger.LogInformation("Migrated settings from legacy path {Legacy} to {New}", legacyPath, _settingsPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to migrate settings from {Legacy}", legacyPath);
                }
            }
        }
    }

    /// <summary>Konstruktor mit explizitem Settings-Pfad (z.B. für Tests). Überspringt Legacy-Migration.</summary>
    public AppSettingsService(ILogger<AppSettingsService> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
        var dir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public AppSettings Current => _current;
    public event Action<AppSettings>? Changed;

    public string ActiveServerDir =>
        _current.Servers.FirstOrDefault(s => s.Id == _current.ActiveServerId)?.InstallDir.TrimEnd('\\', '/')
        ?? string.Empty;

    public async Task SelectServerAsync(string serverId)
    {
        AppSettings changed;
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var next = CloneSettings(_current);
            next.ActiveServerId = serverId;
            await SaveCoreAsync(next).ConfigureAwait(false);
            _current = next;
            changed = CloneSettings(next);
        }
        finally { _lock.Release(); }

        Changed?.Invoke(changed);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var needsResave = false;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("No settings file found, using defaults: {Path}", _settingsPath);
                MigrateToV12(_current);
                return;
            }
            await using var stream = File.OpenRead(_settingsPath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct)
                          .ConfigureAwait(false);
            if (loaded is not null)
            {
                _current = loaded;
                _logger.LogInformation("Settings loaded from {Path}", _settingsPath);

                // Einmalige Migration — alter Default 30s → neuer Default 5s
                // (Windrose kennt keinen Soft-Shutdown, Wartezeit ist sinnlos).
                if (_current.GracefulShutdownSeconds > 15)
                {
                    _logger.LogInformation("Migrating GracefulShutdownSeconds from {Old}s to 5s", _current.GracefulShutdownSeconds);
                    _current.GracefulShutdownSeconds = 5;
                    needsResave = true;
                }

                // Normalize trailing backslashes from all path keys/values (can happen from old saves).
                NormalizePathKeys(_current);

                // Phase 9 (v1.2): seed OptInState für alle bekannten Server.
                MigrateToV12(_current);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, falling back to defaults");
        }
        finally
        {
            _lock.Release();
        }

        if (needsResave)
        {
            try { await SaveAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist migrated settings"); }
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Atomic write: erst in .tmp schreiben, dann per Move ersetzen.
            // Verhindert korrupte Settings bei Crash während des Schreibens.
            await SaveCoreAsync(_current, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        AppSettings changed;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var next = CloneSettings(_current);
            mutate(next);
            await SaveCoreAsync(next, ct).ConfigureAwait(false);
            _current = next;
            changed = CloneSettings(next);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke(changed);
    }

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken ct = default)
    {
        var tmpPath = _settingsPath + ".tmp";
        await using (var stream = File.Create(tmpPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        File.Move(tmpPath, _settingsPath, overwrite: true);
        _logger.LogDebug("Settings saved to {Path}", _settingsPath);
    }

    private static AppSettings CloneSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    /// <summary>
    /// Trim trailing backslashes/slashes from all path-keyed dictionaries and server entries.
    /// Guards against stale settings.json files that were saved with trailing backslashes.
    /// </summary>
    private static void NormalizePathKeys(AppSettings s)
    {
        static string Norm(string p) => p.TrimEnd('\\', '/');

        foreach (var entry in s.Servers)
            entry.InstallDir = Norm(entry.InstallDir);

        if (!string.IsNullOrEmpty(s.ServerInstallDir))
            s.ServerInstallDir = Norm(s.ServerInstallDir);

        NormalizeDictKeys(s.WindrosePlusActiveByServer);
        NormalizeDictKeys(s.WindrosePlusOptInStateByServer);
        NormalizeDictKeys(s.WindrosePlusRconPasswordByServer);
        NormalizeDictKeys(s.WindrosePlusDashboardPortByServer);
        NormalizeDictKeys(s.WindrosePlusAdminSteamIdByServer);
    }

    private static void NormalizeDictKeys<TVal>(Dictionary<string, TVal> dict)
    {
        var keysToNorm = dict.Keys.Where(k => k.EndsWith('\\') || k.EndsWith('/')).ToList();
        foreach (var key in keysToNorm)
        {
            var normed = key.TrimEnd('\\', '/');
            if (!dict.ContainsKey(normed))
                dict[normed] = dict[key];
            dict.Remove(key);
        }
    }

    /// <summary>
    /// Phase 9 (v1.2): seed <see cref="OptInState.NeverAsked"/> für jeden bekannten Server,
    /// der noch keinen expliziten Opt-in-Eintrag hat. Idempotent — überschreibt niemals
    /// existierende <c>OptedIn</c>/<c>OptedOut</c>-Entscheidungen und entfernt keine Orphan-Keys.
    /// </summary>
    private static void MigrateToV12(AppSettings s)
    {
        // Seed OptInState for all known servers that have no explicit entry.
        foreach (var serverDir in s.WindrosePlusActiveByServer.Keys.ToList())
        {
            if (!s.WindrosePlusOptInStateByServer.ContainsKey(serverDir))
                s.WindrosePlusOptInStateByServer[serverDir] = OptInState.NeverAsked;
        }

        // Migrate legacy single-server to multi-server list.
        if (s.Servers.Count == 0 && !string.IsNullOrWhiteSpace(s.ServerInstallDir))
        {
            var entry = new ServerEntry
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = Path.GetFileName(s.ServerInstallDir.TrimEnd('/', '\\')) is { Length: > 0 } n ? n : "Server",
                InstallDir = s.ServerInstallDir,
            };
            s.Servers.Add(entry);
            s.ActiveServerId = entry.Id;
        }
        else if (s.Servers.Count > 0 && string.IsNullOrEmpty(s.ActiveServerId))
        {
            s.ActiveServerId = s.Servers[0].Id;
        }
    }
}
