using System.Text.Json.Serialization;

namespace WindroseServerManager.Core.Models;

/// <summary>
/// Per-server WindrosePlus opt-in state. Serialized as string for human-readable JSON.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OptInState
{
    NeverAsked,
    OptedIn,
    OptedOut
}
