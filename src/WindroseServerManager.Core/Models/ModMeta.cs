namespace WindroseServerManager.Core.Models;

/// <summary>
/// Side-car-Metadaten für eine installierte Mod — verlinkt mit einer Nexus-Mods-Seite.
/// Wird als {modname}.pak.meta.json neben der .pak gespeichert.
/// </summary>
public sealed record ModMeta(
    int NexusModId,
    string LinkedVersion,
    string LinkedDisplayName,
    DateTime LinkedAtUtc);
