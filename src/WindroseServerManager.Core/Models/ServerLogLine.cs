namespace WindroseServerManager.Core.Models;

public enum LogStream
{
    Stdout,
    Stderr,
    System,
}

public sealed record ServerLogLine(DateTime TimestampUtc, LogStream Stream, string Text)
{
    public static ServerLogLine System(string text) =>
        new(DateTime.UtcNow, LogStream.System, text);
}
