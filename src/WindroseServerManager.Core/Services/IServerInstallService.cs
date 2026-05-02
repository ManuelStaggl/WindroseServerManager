using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IServerInstallService
{
    Task<ServerInstallInfo> GetInstallInfoAsync(string installDir, CancellationToken ct = default);

    IAsyncEnumerable<InstallProgress> InstallOrUpdateAsync(
        string installDir,
        CancellationToken ct = default);

    /// <summary>Validates the install directory for writability and sanity. Returns an error message or null if OK.</summary>
    string? ValidateInstallDir(string installDir);

    /// <summary>
    /// Bootet den frisch installierten Windrose-Server einmalig headless, damit er seine
    /// <c>R5/ServerDescription.json</c> mit gültiger <c>DeploymentId</c> + <c>PersistentServerId</c>
    /// selbst erzeugt. Danach kann der Aufrufer den Wunsch-ServerName in die Datei patchen.
    /// Ohne diesen Init-Run verwirft der Server jede vorab geschriebene Datei beim ersten Start
    /// und unsere Einstellungen wären verloren.
    /// Gibt true zurück, wenn die Datei mit einer nicht-leeren DeploymentId existiert.
    /// </summary>
    Task<bool> InitializeServerDescriptionAsync(string installDir, CancellationToken ct = default);

    /// <summary>
    /// Prüft via Steam-API ob eine neuere Build-Version des Windrose Servers verfügbar ist.
    /// Liest den buildId aus der lokalen appmanifest Datei und fragt die Steam API ab.
    /// </summary>
    Task<bool> IsUpdateAvailableAsync(string installDir, CancellationToken ct = default);
}
