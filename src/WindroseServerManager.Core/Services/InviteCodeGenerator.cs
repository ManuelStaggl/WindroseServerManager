using System.Security.Cryptography;
using System.Text;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Erzeugt Windrose-Invite-Codes mit nautisch/piraten-Wortschatz.
/// Regel laut Server: min. 6 Zeichen, nur 0-9 a-z A-Z, case-sensitive.
/// </summary>
public static class InviteCodeGenerator
{
    // Kurze Wörter mit Piraten-/Seefahrer-Charme, alle ASCII-Buchstaben.
    private static readonly string[] Words =
    {
        "Ahoy", "Anchor", "Bay", "Brig", "Crew", "Cove", "Coral", "Deck",
        "Dock", "Drake", "Foam", "Gale", "Gold", "Helm", "Hook", "Isle",
        "Keel", "Knot", "Mast", "Mate", "Mist", "Moon", "Ocean", "Pearl",
        "Pier", "Pirate", "Port", "Raft", "Reef", "Rope", "Rudder", "Rum",
        "Sail", "Salt", "Sand", "Shark", "Shanty", "Ship", "Shore", "Siren",
        "Skull", "Sloop", "Star", "Storm", "Surf", "Swell", "Tide", "Wave",
        "Wind", "Wreck", "Voyage", "Treasure", "Galley", "Cannon", "Cutlass",
    };

    // [0-9a-zA-Z] ohne verwechselbare Zeichen (0/O, 1/l/I)
    private static readonly char[] SuffixChars = "23456789abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ".ToCharArray();

    /// <summary>
    /// Erzeugt einen neuen Invite-Code im Format "WordWordXX" (zwei Wörter + 2 Zeichen).
    /// Länge liegt typischerweise zwischen 8 und 14 Zeichen.
    /// </summary>
    public static string Generate()
    {
        var w1 = PickWord();
        var w2 = PickWord();
        while (string.Equals(w1, w2, StringComparison.OrdinalIgnoreCase))
            w2 = PickWord();

        var sb = new StringBuilder(16);
        sb.Append(w1).Append(w2);
        sb.Append(PickChar(SuffixChars));
        sb.Append(PickChar(SuffixChars));
        return sb.ToString();
    }

    private static string PickWord() => Words[RandomNumberGenerator.GetInt32(Words.Length)];
    private static char PickChar(char[] pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];
}
