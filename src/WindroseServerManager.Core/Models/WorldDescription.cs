using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindroseServerManager.Core.Models;

public enum WorldPresetType
{
    Easy,
    Medium,
    Hard,
    Custom,
}

/// <summary>
/// Envelope matching the real Windrose WorldDescription.json:
/// { "Version": 1, "WorldDescription": { ... } }
/// Unknown top-level keys are preserved round-trip.
/// </summary>
public sealed class WorldDescriptionFile
{
    public int Version { get; set; } = 1;

    [JsonPropertyName("WorldDescription")]
    public WorldDescription Inner { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Inner per-world settings. Unknown keys are preserved.
/// Note: the server writes <c>islandId</c> with a lowercase 'i'.
/// </summary>
public sealed class WorldDescription
{
    [JsonPropertyName("islandId")]
    public string IslandId { get; set; } = string.Empty;

    public string WorldName { get; set; } = "Neue Welt";

    /// <summary>.NET DateTime.Ticks as double — matches server-written format.</summary>
    public double CreationTime { get; set; }

    public WorldPresetType WorldPresetType { get; set; } = WorldPresetType.Medium;

    public WorldSettings WorldSettings { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Tag-parameter value wrapper. The server writes each tag value as
/// <c>{ "TagName": "WDS.Parameter.CombatDifficulty.Normal" }</c>, not a bare string.
/// </summary>
public sealed class TagValue
{
    public string TagName { get; set; } = string.Empty;

    public TagValue() { }
    public TagValue(string tagName) { TagName = tagName; }
}

public sealed class WorldSettings
{
    public Dictionary<string, bool> BoolParameters { get; set; } = new();
    public Dictionary<string, double> FloatParameters { get; set; } = new();
    public Dictionary<string, TagValue> TagParameters { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Known World-Parameter keys, labels and ranges.
/// Keys are stored as the literal JSON string the server writes:
/// <c>{"TagName": "WDS.Parameter.Xyz"}</c>.
/// </summary>
public static class WorldParameterCatalog
{
    public static string MakeKey(string tagName) => "{\"TagName\": \"" + tagName + "\"}";

    // Bool keys
    public const string CoopSharedQuests = "WDS.Parameter.Coop.SharedQuests";
    public const string EasyExplore      = "WDS.Parameter.EasyExplore";

    // Float keys
    public const string MobHealth        = "WDS.Parameter.MobHealthMultiplier";
    public const string MobDamage        = "WDS.Parameter.MobDamageMultiplier";
    public const string ShipsHealth      = "WDS.Parameter.ShipsHealthMultiplier";
    public const string ShipsDamage      = "WDS.Parameter.ShipsDamageMultiplier";
    public const string BoardingDiff     = "WDS.Parameter.BoardingDifficultyMultiplier";
    public const string CoopStatsCorr    = "WDS.Parameter.Coop.StatsCorrectionModifier";
    public const string CoopShipStatsCorr= "WDS.Parameter.Coop.ShipStatsCorrectionModifier";

    // Tag keys
    public const string CombatDifficulty = "WDS.Parameter.CombatDifficulty";

    // Tag values for CombatDifficulty
    public const string CombatEasy   = "WDS.Parameter.CombatDifficulty.Easy";
    public const string CombatNormal = "WDS.Parameter.CombatDifficulty.Normal";
    public const string CombatHard   = "WDS.Parameter.CombatDifficulty.Hard";

    public static readonly IReadOnlyList<string> KnownBoolKeys = new[]
    {
        CoopSharedQuests, EasyExplore,
    };

    public static readonly IReadOnlyList<string> KnownFloatKeys = new[]
    {
        MobHealth, MobDamage, ShipsHealth, ShipsDamage,
        BoardingDiff, CoopStatsCorr, CoopShipStatsCorr,
    };

    public static readonly IReadOnlyList<string> KnownTagKeys = new[]
    {
        CombatDifficulty,
    };

    private static readonly Dictionary<string, string> Labels = new()
    {
        [CoopSharedQuests]  = "Quest-Fortschritt zwischen Coop-Spielern teilen",
        [EasyExplore]       = "Karte ohne Marker (erhöht Entdeckungs-Spaß)",
        [MobHealth]         = "Gegner-Lebenspunkte (1,0 = Standard)",
        [MobDamage]         = "Gegner-Schaden (1,0 = Standard)",
        [ShipsHealth]       = "Schiffs-HP (Feinde)",
        [ShipsDamage]       = "Schiffs-Schaden (Feinde)",
        [BoardingDiff]      = "Enter-Schwierigkeit",
        [CoopStatsCorr]     = "Coop: Gegner-Skalierung pro Spieler",
        [CoopShipStatsCorr] = "Coop: Schiff-Skalierung pro Spieler",
        [CombatDifficulty]  = "Kampf-Schwierigkeit",
    };

    public static string GetLabel(string key) => Labels.TryGetValue(key, out var l) ? l : key;

    public static (double Min, double Max, double Default) GetRange(string floatKey) => floatKey switch
    {
        MobHealth         => (0.2, 5.0, 1.0),
        MobDamage         => (0.2, 5.0, 1.0),
        ShipsHealth       => (0.4, 5.0, 1.0),
        ShipsDamage       => (0.2, 2.5, 1.0),
        BoardingDiff      => (0.2, 5.0, 1.0),
        CoopStatsCorr     => (0.0, 2.0, 1.0),
        CoopShipStatsCorr => (0.0, 2.0, 0.0),
        _                 => (0.0, 5.0, 1.0),
    };

    public static bool GetBoolDefault(string boolKey) => boolKey switch
    {
        CoopSharedQuests => true,
        EasyExplore      => false,
        _                => false,
    };
}
