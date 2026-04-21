using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }
    event Action<AppSettings>? Changed;

    /// <summary>InstallDir des aktiven Servers. Leer wenn kein Server konfiguriert.</summary>
    string ActiveServerDir { get; }

    /// <summary>Aktiven Server wechseln und Changed-Event feuern.</summary>
    Task SelectServerAsync(string serverId);

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default);
}
