namespace WindroseServerManager.App.Services;

/// <summary>
/// Ergebnis eines App-Update-Checks gegen die GitHub Releases API.
/// </summary>
/// <param name="HasUpdate">True wenn die aktuelle Assembly-Version kleiner ist als das neueste Release.</param>
/// <param name="CurrentVersion">Normalisierte Version der laufenden App (z.B. "1.0.0").</param>
/// <param name="LatestVersion">Tag-Name des neuesten Releases ohne führendes "v" (z.B. "1.0.1"), falls verfügbar.</param>
/// <param name="ReleaseUrl">HTML-Seite des Releases auf GitHub, falls verfügbar.</param>
/// <param name="DownloadUrl">Direkter Download-Link zum Installer-Asset, falls vorhanden.</param>
/// <param name="Message">Menschenlesbare Zusammenfassung für Toast/Status-Text.</param>
public sealed record AppUpdateResult(
    bool HasUpdate,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? DownloadUrl,
    string Message);

public interface IAppUpdateService
{
    /// <summary>Fired every time CheckAsync completes with a result (auch ohne Update, damit UI "aktuell"-Status zeigen kann).</summary>
    event Action<AppUpdateResult>? UpdateChecked;

    Task<AppUpdateResult> CheckAsync(CancellationToken ct = default);
}
