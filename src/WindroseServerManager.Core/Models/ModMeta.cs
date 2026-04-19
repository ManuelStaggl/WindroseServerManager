namespace WindroseServerManager.Core.Models;

/// <summary>
/// Side-car-Metadaten für eine installierte Mod — merkt sich die Nexus-Mod-ID
/// damit der "Auf Nexus öffnen"-Button auf die richtige Seite springen kann.
/// Wird als {modname}.pak.meta.json neben der .pak gespeichert.
/// </summary>
public sealed record ModMeta(
    int NexusModId,
    DateTime LinkedAtUtc);
