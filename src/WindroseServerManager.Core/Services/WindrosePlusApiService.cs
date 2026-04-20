using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class WindrosePlusApiService : IWindrosePlusApiService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindrosePlusApiService> _logger;

    public WindrosePlusApiService(
        IHttpClientFactory httpFactory,
        IAppSettingsService settings,
        ILogger<WindrosePlusApiService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    private int GetPort(string serverDir) =>
        _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);

    private string GetPassword(string serverDir) =>
        _settings.Current.WindrosePlusRconPasswordByServer.GetValueOrDefault(serverDir, string.Empty);

    public async Task<string?> RconAsync(string serverDir, string command, CancellationToken ct = default)
    {
        var port = GetPort(serverDir);
        if (port <= 0)
            return null;

        var password = GetPassword(serverDir);
        var body = JsonSerializer.Serialize(new { password, command });
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var client = _httpFactory.CreateClient();
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client
                .PostAsync($"http://localhost:{port}/api/rcon", content, linked.Token)
                .ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                    return msgEl.GetString();
            }
            catch (JsonException)
            {
                // Not JSON — return raw body
            }

            return responseBody;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "WindrosePlusApiService.RconAsync failed for port {Port}", port);
            return null;
        }
    }

    public async Task<WindrosePlusStatusResult?> GetStatusAsync(string serverDir, CancellationToken ct = default)
    {
        var port = GetPort(serverDir);
        if (port <= 0)
            return null;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var client = _httpFactory.CreateClient();
            using var response = await client
                .GetAsync($"http://localhost:{port}/api/status", linked.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            return ParseStatusResult(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "WindrosePlusApiService.GetStatusAsync failed for port {Port}", port);
            return null;
        }
    }

    private static WindrosePlusStatusResult ParseStatusResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? serverName = null;
        if (root.TryGetProperty("serverName", out var snEl) && snEl.ValueKind == JsonValueKind.String)
            serverName = snEl.GetString();

        string? wpVersion = null;
        if (root.TryGetProperty("windrosePlusVersion", out var vEl) && vEl.ValueKind == JsonValueKind.String)
            wpVersion = vEl.GetString();

        var players = new List<WindrosePlusPlayer>();
        if (root.TryGetProperty("players", out var playersEl) && playersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in playersEl.EnumerateArray())
                players.Add(ParsePlayer(p));
        }

        return new WindrosePlusStatusResult(players, serverName, wpVersion);
    }

    public async Task<WindrosePlusQueryResult?> QueryAsync(string serverDir, CancellationToken ct = default)
    {
        var port = GetPort(serverDir);
        if (port <= 0)
            return null;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var client = _httpFactory.CreateClient();
            using var response = await client
                .GetAsync($"http://localhost:{port}/query", linked.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var players = new List<WindrosePlusPlayer>();
            if (root.TryGetProperty("players", out var playersEl) && playersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in playersEl.EnumerateArray())
                    players.Add(ParsePlayer(p));
            }

            return new WindrosePlusQueryResult(players);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "WindrosePlusApiService.QueryAsync failed for port {Port}", port);
            return null;
        }
    }

    private static WindrosePlusPlayer ParsePlayer(JsonElement p)
    {
        var steamId = p.TryGetProperty("steamId", out var sidEl) ? sidEl.GetString() ?? string.Empty : string.Empty;
        var name    = p.TryGetProperty("name",    out var nEl)   ? nEl.GetString()   ?? string.Empty : string.Empty;
        var alive   = p.TryGetProperty("alive",   out var aEl)   && aEl.ValueKind == JsonValueKind.True;

        int sessionSeconds = 0;
        if (p.TryGetProperty("sessionSeconds", out var ssEl) && ssEl.TryGetInt32(out var ss))
            sessionSeconds = ss;
        else if (p.TryGetProperty("playtime", out var ptEl) && ptEl.TryGetInt32(out var pt))
            sessionSeconds = pt;

        double? worldX = null;
        if (p.TryGetProperty("worldX", out var wxEl) && wxEl.TryGetDouble(out var wx))
            worldX = wx;

        double? worldY = null;
        if (p.TryGetProperty("worldY", out var wyEl) && wyEl.TryGetDouble(out var wy))
            worldY = wy;

        string? shipInfo = null;
        if (p.TryGetProperty("shipInfo", out var siEl) && siEl.ValueKind == JsonValueKind.String)
            shipInfo = siEl.GetString();

        return new WindrosePlusPlayer(steamId, name, alive, sessionSeconds, worldX, worldY, shipInfo);
    }

    public WindrosePlusConfig? ReadConfig(string serverDir)
    {
        var configPath = Path.Combine(serverDir, "windrose_plus.json");
        if (!File.Exists(configPath))
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<WindrosePlusConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WindrosePlusApiService.ReadConfig failed for {Path}", configPath);
            return null;
        }
    }

    public async Task WriteConfigAsync(string serverDir, WindrosePlusConfig config, CancellationToken ct = default)
    {
        var configPath = Path.Combine(serverDir, "windrose_plus.json");
        var tmpPath = configPath + ".tmp";

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tmpPath, json, ct).ConfigureAwait(false);
        File.Move(tmpPath, configPath, overwrite: true);
    }

    public string BuildKickCommand(string identifier) => $"wp.kick {identifier}";

    public string BuildBanCommand(string identifier, int? minutes) =>
        minutes.HasValue ? $"wp.ban {identifier} {minutes.Value}" : $"wp.ban {identifier}";

    public string BuildBroadcastCommand(string message) => $"wp.say {message}";
}
