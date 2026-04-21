using System.Globalization;
using System.Text.Json;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public static class EventsLogParser
{
    private static readonly HashSet<string> ValidTypes =
        new(StringComparer.OrdinalIgnoreCase) { "join", "leave" };

    public static WindrosePlusEvent? TryParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl))
                return null;

            var type = typeEl.GetString();
            if (type is null || !ValidTypes.Contains(type))
                return null;

            // WindrosePlus events.log has no steamId field
            string? steamId = null;

            // WindrosePlus writes "player" for the name field
            var name = "Unknown";
            foreach (var field in new[] { "player", "name" })
            {
                if (root.TryGetProperty(field, out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                {
                    var parsedName = nameEl.GetString();
                    if (!string.IsNullOrEmpty(parsedName)) { name = parsedName; break; }
                }
            }

            // WindrosePlus writes "ts" as a Unix timestamp integer
            var timestamp = DateTime.UtcNow;
            if (root.TryGetProperty("ts", out var tsUnixEl) && tsUnixEl.TryGetInt64(out var unixSec))
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSec).UtcDateTime;
            }
            else if (root.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.String)
            {
                var tsStr = tsEl.GetString();
                if (!string.IsNullOrEmpty(tsStr) &&
                    DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                {
                    timestamp = parsed;
                }
            }

            return new WindrosePlusEvent(type, steamId, name, timestamp);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool MatchesFilter(WindrosePlusEvent evt, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        if (evt.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (evt.Type.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (evt.SteamId is not null && evt.SteamId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
