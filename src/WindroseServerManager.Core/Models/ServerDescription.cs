using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindroseServerManager.Core.Models;

/// <summary>
/// Envelope matching the real Windrose ServerDescription.json:
/// { "Version": 1, "DeploymentId": "...", "ServerDescription_Persistent": { ... } }
/// Unknown top-level keys are preserved round-trip.
/// </summary>
public sealed class ServerDescriptionFile
{
    public int Version { get; set; } = 1;

    public string DeploymentId { get; set; } = string.Empty;

    [JsonPropertyName("ServerDescription_Persistent")]
    public ServerDescription Persistent { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Inner persistent part of ServerDescription.json. Unknown keys are preserved round-trip.
/// </summary>
public sealed class ServerDescription
{
    public string ServerName { get; set; } = "Windrose Server";
    public string InviteCode { get; set; } = string.Empty;
    public bool IsPasswordProtected { get; set; }
    public string Password { get; set; } = string.Empty;
    public int MaxPlayerCount { get; set; } = 8;
    public string P2pProxyAddress { get; set; } = string.Empty;
    public string PersistentServerId { get; set; } = string.Empty;
    public string WorldIslandId { get; set; } = string.Empty;

    /// <summary>Catch-all for unknown fields so we preserve them on save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// Validates an InviteCode. Rules: min. 6 chars, only [0-9a-zA-Z], case-sensitive.
    /// Returns null if valid, otherwise a user-facing error message (German).
    /// Empty is treated as valid (no code = no lock-in).
    /// </summary>
    public static string? ValidateInviteCode(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        if (code.Length < 6) return "Invite-Code muss mindestens 6 Zeichen lang sein.";
        foreach (var c in code)
        {
            var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            if (!ok) return "Invite-Code darf nur 0-9, a-z und A-Z enthalten.";
        }
        return null;
    }
}
