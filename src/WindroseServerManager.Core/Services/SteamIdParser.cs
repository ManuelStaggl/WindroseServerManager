using System.Text.RegularExpressions;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Extrahiert eine SteamID64 aus einer SteamID-Eingabe (raw 17-digit oder /profiles/-URL).
/// Vanity-URLs (/id/name) werden verworfen — Auflösung bräuchte Steam API.
/// </summary>
public static class SteamIdParser
{
    private static readonly Regex RawSteamId = new(
        @"^7656119\d{10}$",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly Regex ProfileUrl = new(
        @"^https?://steamcommunity\.com/profiles/(7656119\d{10})/?(\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Gibt die SteamID64 zurück wenn Input ein gültiges Format ist, sonst <c>null</c>.
    /// Akzeptiert: raw 17-digit, https?://steamcommunity.com/profiles/{id}[/][?...].
    /// Lehnt ab: Vanity-URLs, falsche Länge, Müll, null/leer.
    /// </summary>
    public static string? ExtractSteamId64(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        if (RawSteamId.IsMatch(trimmed)) return trimmed;
        var m = ProfileUrl.Match(trimmed);
        return m.Success ? m.Groups[1].Value : null;
    }
}
