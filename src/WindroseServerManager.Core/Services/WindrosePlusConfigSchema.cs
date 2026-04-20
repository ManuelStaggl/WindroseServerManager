using System.Globalization;

namespace WindroseServerManager.Core.Services;

public sealed record ConfigEntrySchema(
    string Category, string Key, string Type,
    double? Min, double? Max, object? Default, string DescriptionKey);

public static class WindrosePlusConfigSchema
{
    public static IReadOnlyList<ConfigEntrySchema> All { get; } = new List<ConfigEntrySchema>
    {
        new("Server", "http_port",         "int",   1024, 65535, 8780,  "Editor.Schema.HttpPort"),
        new("Server", "rcon_enabled",      "bool",  null, null,  false, "Editor.Schema.RconEnabled"),
        new("Server", "rcon_password",     "string",null, null,  "",    "Editor.Schema.RconPassword"),
        new("Multipliers", "xp",              "float", 0.1, 100, 1.0, "Editor.Schema.Xp"),
        new("Multipliers", "loot",            "float", 0.1, 100, 1.0, "Editor.Schema.Loot"),
        new("Multipliers", "stack_size",      "float", 0.1, 100, 1.0, "Editor.Schema.StackSize"),
        new("Multipliers", "craft_cost",      "float", 0.1, 100, 1.0, "Editor.Schema.CraftCost"),
        new("Multipliers", "crop_speed",      "float", 0.1, 100, 1.0, "Editor.Schema.CropSpeed"),
        new("Multipliers", "cooking_speed",   "float", 0.1, 100, 1.0, "Editor.Schema.CookingSpeed"),
        new("Multipliers", "harvest_yield",   "float", 0.1, 100, 1.0, "Editor.Schema.HarvestYield"),
        new("Multipliers", "inventory_size",  "float", 0.1, 100, 1.0, "Editor.Schema.InventorySize"),
        new("Multipliers", "points_per_level","float", 0.1, 100, 1.0, "Editor.Schema.PointsPerLevel"),
        new("Multipliers", "weight",          "float", 0.1, 100, 1.0, "Editor.Schema.Weight"),
    };

    public static string? Validate(string key, string rawValue)
    {
        var schema = All.FirstOrDefault(s => s.Key == key);
        if (schema is null) return $"Unknown key: {key}";
        switch (schema.Type)
        {
            case "float":
                if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return "Not a number";
                if (schema.Min.HasValue && f < schema.Min.Value) return $"Min {schema.Min}";
                if (schema.Max.HasValue && f > schema.Max.Value) return $"Max {schema.Max}";
                return null;
            case "int":
                if (!int.TryParse(rawValue, out var i)) return "Not an integer";
                if (schema.Min.HasValue && i < schema.Min.Value) return $"Min {schema.Min}";
                if (schema.Max.HasValue && i > schema.Max.Value) return $"Max {schema.Max}";
                return null;
            case "bool":
                if (!bool.TryParse(rawValue, out _)) return "Not a boolean";
                return null;
            default:
                return null;
        }
    }
}
