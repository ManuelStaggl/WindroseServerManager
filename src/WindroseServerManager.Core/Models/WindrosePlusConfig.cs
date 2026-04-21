using System.Text.Json.Serialization;

namespace WindroseServerManager.Core.Models;

public sealed class WindrosePlusConfig
{
    [JsonPropertyName("server")]
    public Dictionary<string, object?> Server { get; set; } = new();

    [JsonPropertyName("rcon")]
    public Dictionary<string, object?> Rcon { get; set; } = new();

    [JsonPropertyName("multipliers")]
    public Dictionary<string, object?> Multipliers { get; set; } = new();
}
