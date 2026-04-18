namespace WindroseServerManager.Core.Models;

public enum ServerEventType
{
    Started,
    Stopped,
    Crashed,
    ScheduledRestart,
    AutoRestartHighRam,
    AutoRestartMaxUptime,
}

/// <summary>
/// Eintrag im Session-Verlauf. Persistiert als JSON-Line unter %AppData%\WindroseServerManager\events.jsonl.
/// </summary>
public sealed record ServerEvent(
    DateTime TimestampUtc,
    ServerEventType Type,
    string Reason,
    int? ExitCode = null,
    TimeSpan? SessionDuration = null);
