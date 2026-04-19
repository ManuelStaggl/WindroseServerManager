using System.Text.RegularExpressions;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Parst eine Steam-Admin-Eingabe: SteamID64 (17-stellig), numerische /profiles/-URL,
/// oder Vanity-URL (/id/name) — die as-is gespeichert wird (keine API-Auflösung).
/// </summary>
public static class SteamIdParser
{
    private static readonly Regex RawSteamId = new(
        @"^7656119\d{10}$",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly Regex ProfileUrl = new(
        @"^https?://steamcommunity\.com/profiles/(7656119\d{10})/?(\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    private static readonly Regex VanityUrl = new(
        @"^(https?://)?steamcommunity\.com/id/[a-zA-Z0-9_-]{2,32}/?(\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Gibt den normalisierten Admin-Bezeichner zurück wenn der Input gültig ist, sonst <c>null</c>.
    /// Rückgabe ist entweder eine 17-stellige SteamID64 oder eine normalisierte https-URL.
    /// </summary>
    public static string? ExtractSteamId64(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();

        if (RawSteamId.IsMatch(trimmed)) return trimmed;

        var m = ProfileUrl.Match(trimmed);
        if (m.Success) return m.Groups[1].Value;

        if (VanityUrl.IsMatch(trimmed))
        {
            // Normalisiere auf https:// wenn fehlendes Schema
            return trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : "https://" + trimmed;
        }

        return null;
    }
}
