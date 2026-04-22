using System.Text.RegularExpressions;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Parser für Nexus-Mods-URLs und reine Mod-IDs.
/// Akzeptiert z.B.:
///   https://www.nexusmods.com/windrose/mods/29
///   http://nexusmods.com/windrose/mods/29?tab=description
///   www.nexusmods.com/windrose/mods/29
///   29
/// </summary>
public static partial class NexusUrlParser
{
    private static readonly Regex UrlRegex = BuildUrlRegex();
    private static readonly Regex ArchiveNameRegex = BuildArchiveNameRegex();

    /// <summary>
    /// Extrahiert die Mod-ID aus einem Nexus-Download-Dateinamen.
    /// Nexus-Schema: "{ModName}-{ModId}-{Version}-{Timestamp}.{ext}" — die erste Zahlenfolge
    /// nach dem letzten Alpha-Block ist die Mod-ID, gefolgt von mindestens zwei weiteren Zahlenblöcken.
    /// Gibt -1 zurück wenn das Muster nicht passt.
    /// </summary>
    public static int TryExtractModIdFromArchiveName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return -1;
        var nameOnly = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var match = ArchiveNameRegex.Match(nameOnly);
        if (!match.Success) return -1;
        return int.TryParse(match.Groups["id"].Value, out var id) ? id : -1;
    }

    /// <summary>
    /// Versucht Mod-ID zu extrahieren. Game-Domain muss zur erwarteten Domain passen
    /// (wir akzeptieren nur Mods für das richtige Spiel).
    /// </summary>
    public static bool TryParse(string input, string expectedDomain, out int modId, out string? reason)
    {
        modId = 0;
        reason = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            reason = "Eingabe ist leer.";
            return false;
        }

        var trimmed = input.Trim();

        // Reine Zahl?
        if (int.TryParse(trimmed, out var asInt) && asInt > 0)
        {
            modId = asInt;
            return true;
        }

        var match = UrlRegex.Match(trimmed);
        if (!match.Success)
        {
            reason = "Not a valid Nexus link. Expected: https://www.nexusmods.com/{domain}/mods/{id}";
            return false;
        }

        var domain = match.Groups["domain"].Value;
        if (!string.Equals(domain, expectedDomain, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Link points to {domain}, expected {expectedDomain}. This mod is not for this game.";
            return false;
        }

        modId = int.Parse(match.Groups["id"].Value);
        return true;
    }

    [GeneratedRegex(
        @"(?:https?://)?(?:www\.)?nexusmods\.com/(?<domain>[a-z0-9\-]+)/mods/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BuildUrlRegex();

    // Matcht Nexus-Archiv-Naming: irgendwas-{modId}-{ver}-{...}-{timestamp}
    // Verankert am Ende: letzter Block ist der Unix-Timestamp (9+ Ziffern),
    // die Mod-ID ist die erste Zahl der Dash-Kette die darauf endet.
    [GeneratedRegex(
        @"-(?<id>\d{1,7})(?:-\d+)+-\d{9,}$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BuildArchiveNameRegex();
}
