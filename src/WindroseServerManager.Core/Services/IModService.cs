using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IModService
{
    /// <summary>Absoluter Pfad des Mods-Verzeichnisses (R5/Content/Paks/~mods). Null wenn Server-Install fehlt.</summary>
    string? GetModsDir();

    /// <summary>Null = ready. Sonst lokalisierbarer Grund (Server läuft / kein Install-Pfad / etc.).</summary>
    string? ValidateReady();

    /// <summary>Scannt ~mods/ und liefert alle .pak / .pak.disabled Einträge inkl. Companion-Files.</summary>
    IEnumerable<ModInfo> ListMods();

    /// <summary>Installiert Mod aus .pak, .zip oder .7z. Liefert alle neu installierten Primär-Paks.</summary>
    Task<IReadOnlyList<ModInfo>> InstallFromArchiveAsync(string sourcePath, CancellationToken ct = default);

    /// <summary>Enable via Rename *.pak.disabled → *.pak (und umgekehrt). Idempotent.</summary>
    void SetEnabled(string fileName, bool enabled);

    /// <summary>Löscht das .pak und alle Companion-Files (gleicher Basename, .ucas/.utoc).</summary>
    void UninstallMod(string fileName);

    /// <summary>Erstellt ein ZIP mit allen AKTIVEN Mods zur Weitergabe an Clients.</summary>
    Task<string> ExportClientBundleAsync(string targetZipPath, CancellationToken ct = default);

    /// <summary>Lädt Side-Car-Metadaten (Nexus-Verknüpfung) für einen Mod. Null wenn nicht verlinkt.</summary>
    ModMeta? GetMeta(string fileName);

    /// <summary>Schreibt/aktualisiert Side-Car-Metadaten — verlinkt einen Mod mit einer Nexus-Seite.</summary>
    void SetMeta(string fileName, ModMeta meta);

    /// <summary>Entfernt die Nexus-Verknüpfung (löscht die Side-Car-Datei).</summary>
    void ClearMeta(string fileName);
}
