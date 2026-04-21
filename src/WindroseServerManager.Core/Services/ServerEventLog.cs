using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Append-only event log, persisted as JSON lines. Keeps I/O cheap and resilient to corruption —
/// a broken line does not destroy the rest of the file.
/// </summary>
public sealed class ServerEventLog : IServerEventLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogger<ServerEventLog> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action<ServerEvent>? Appended;

    public ServerEventLog(ILogger<ServerEventLog> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindroseServerManager");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "events.jsonl");
    }

    public async Task AppendAsync(ServerEvent evt, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(evt, JsonOptions);
            await File.AppendAllTextAsync(_filePath, json + Environment.NewLine, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot append server event to {Path}", _filePath);
        }
        finally
        {
            _lock.Release();
        }

        try { Appended?.Invoke(evt); }
        catch (Exception ex) { _logger.LogDebug(ex, "Event subscriber threw"); }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(_filePath))
                await File.WriteAllTextAsync(_filePath, string.Empty, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot clear event log {Path}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ServerEvent>> ReadRecentAsync(int maxCount = 100, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<ServerEvent>();

            // Tail-read: gesamte Datei einlesen, letzte N parsen. Events sind klein, das ist ok.
            var lines = await File.ReadAllLinesAsync(_filePath, ct).ConfigureAwait(false);
            var take = Math.Min(maxCount, lines.Length);
            var result = new List<ServerEvent>(take);

            for (int i = lines.Length - take; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var evt = JsonSerializer.Deserialize<ServerEvent>(line, JsonOptions);
                    if (evt is not null) result.Add(evt);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping corrupt event line");
                }
            }

            // Neueste zuerst.
            result.Reverse();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot read event log {Path}", _filePath);
            return Array.Empty<ServerEvent>();
        }
        finally
        {
            _lock.Release();
        }
    }
}
