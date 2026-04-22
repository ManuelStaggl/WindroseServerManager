namespace WindroseServerManager.App.Services;

/// <summary>
/// Status eines einzelnen Servers beim WindrosePlus-Update-Check.
/// </summary>
/// <param name="ServerId">Id aus <c>ServerEntry</c>.</param>
/// <param name="ServerName">Anzeigename des Servers.</param>
/// <param name="InstallDir">Absoluter Server-Install-Pfad.</param>
/// <param name="InstalledTag">Tag aus <c>.wplus-version</c> (z.B. "v1.2.0"), <c>null</c> wenn kein Marker.</param>
/// <param name="HasUpdate">True wenn <paramref name="InstalledTag"/> vom Latest-Tag abweicht.</param>
public sealed record WindrosePlusServerStatus(
    string ServerId,
    string ServerName,
    string InstallDir,
    string? InstalledTag,
    bool HasUpdate);

/// <summary>
/// Ergebnis eines WindrosePlus-Update-Checks über alle aktivierten Server.
/// </summary>
/// <param name="AnyUpdate">True wenn mindestens ein Server ein Update benötigt.</param>
/// <param name="LatestTag">Neuestes GitHub-Release-Tag oder <c>null</c> bei Fehler.</param>
/// <param name="ReleaseUrl">HTML-URL des Latest-Release auf GitHub (Release-Notes).</param>
/// <param name="Servers">Status pro opt-in Server (leer, wenn keine Server opted-in).</param>
/// <param name="Message">Menschenlesbare Zusammenfassung.</param>
/// <param name="Succeeded">False, wenn der Check komplett fehlschlug (Netzwerk/Parse).</param>
/// <param name="CheckedUtc">UTC-Zeitpunkt des Checks.</param>
public sealed record WindrosePlusUpdateResult(
    bool AnyUpdate,
    string? LatestTag,
    string? ReleaseUrl,
    IReadOnlyList<WindrosePlusServerStatus> Servers,
    string Message,
    bool Succeeded,
    DateTime CheckedUtc);

public interface IWindrosePlusUpdateService
{
    /// <summary>Letztes Ergebnis (oder <c>null</c> wenn noch kein Check lief).</summary>
    WindrosePlusUpdateResult? LastResult { get; }

    /// <summary>Wird bei jedem erfolgreichen oder gescheiterten Check gefeuert.</summary>
    event Action<WindrosePlusUpdateResult>? UpdateChecked;

    Task<WindrosePlusUpdateResult> CheckAsync(CancellationToken ct = default);
}
