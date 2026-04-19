using System.Security.Cryptography;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Erzeugt URL-safe RCON-Passwörter (nur [A-Za-z0-9-_]).
/// Verwendet <see cref="RandomNumberGenerator"/> für kryptografische Zufälligkeit.
/// </summary>
public static class RconPasswordGenerator
{
    private const string UrlSafeAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>Erzeugt ein URL-safe Passwort der angegebenen Länge (>= 16).</summary>
    public static string Generate(int length = 24)
    {
        if (length < 16)
            throw new ArgumentOutOfRangeException(nameof(length), "Minimum 16 chars for security.");

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = UrlSafeAlphabet[bytes[i] % UrlSafeAlphabet.Length];
        return new string(chars);
    }
}
