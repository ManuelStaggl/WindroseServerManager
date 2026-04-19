namespace WindroseServerManager.Core.Models;

/// <summary>
/// Ausschnitt aus der Nexus-Mods-API-Antwort (/v1/games/{domain}/mods/{id}.json).
/// Nur die Felder, die wir wirklich brauchen — alles andere wird ignoriert.
/// </summary>
public sealed record NexusModInfo(
    int ModId,
    string Name,
    string Version,
    string Summary,
    string? PictureUrl,
    string DomainName,
    bool Available)
{
    /// <summary>Browserbarer Link zur Mod-Seite auf nexusmods.com.</summary>
    public string ModPageUrl => $"https://www.nexusmods.com/{DomainName}/mods/{ModId}";
}
