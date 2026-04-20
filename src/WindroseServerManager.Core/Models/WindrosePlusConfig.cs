using System.Text.Json.Serialization;

namespace WindroseServerManager.Core.Models;

public sealed class WindrosePlusConfig
{
    [JsonPropertyName("Server")]
    public Dictionary<string, object?> Server { get; set; } = new();

    [JsonPropertyName("Multipliers")]
    public Dictionary<string, object?> Multipliers { get; set; } = new();
}
