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

    public AppSettings Current => _current;
    public event Action<AppSettings>? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var needsResave = false;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("No settings file found, using defaults: {Path}", _settingsPath);
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
            var tmpPath = _settingsPath + ".tmp";
            await using (var stream = File.Create(tmpPath))
            {
                await JsonSerializer.SerializeAsync(stream, _current, JsonOptions, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
            File.Move(tmpPath, _settingsPath, overwrite: true);
            _logger.LogDebug("Settings saved to {Path}", _settingsPath);
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
        mutate(_current);
        await SaveAsync(ct).ConfigureAwait(false);
        Changed?.Invoke(_current);
    }
}
