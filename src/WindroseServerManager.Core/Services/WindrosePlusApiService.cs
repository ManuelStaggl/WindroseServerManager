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

    // Session cache: serverDir → (cookie value, expiry)
    private readonly Dictionary<string, (string Cookie, DateTime Expiry)> _sessionCache = new();
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

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

    private string GetPassword(string serverDir)
    {
        // Read from windrose_plus.json first — this is the source of truth the PS server uses.
        var config = ReadConfig(serverDir);
        if (config?.Rcon.TryGetValue("password", out var pw) == true && pw is not null)
        {
            var s = pw is System.Text.Json.JsonElement el ? el.GetString() : pw as string;
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return _settings.Current.WindrosePlusRconPasswordByServer.GetValueOrDefault(serverDir, string.Empty);
    }

    /// <summary>
    /// POST /login with form data to get a wp_session cookie.
    /// Returns the raw cookie value or null on failure.
    /// </summary>
    private async Task<string?> LoginAsync(int port, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(password)) return null;
        try
        {
            // Use a plain handler — no auto-redirect so we can read the Set-Cookie header.
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            var form = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("password", password) });
            using var resp = await client.PostAsync($"http://localhost:{port}/login", form, ct).ConfigureAwait(false);

            // Extract wp_session from Set-Cookie header (present on 302 redirect after successful login)
            if (resp.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    if (cookie.StartsWith("wp_session=", StringComparison.OrdinalIgnoreCase))
                        return cookie.Split(';')[0]["wp_session=".Length..];
                }
            }
            _logger.LogWarning("WindrosePlusApiService: login to port {Port} returned {Status} with no session cookie", port, (int)resp.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WindrosePlusApiService: login to port {Port} failed", port);
            return null;
        }
    }

    /// <summary>Returns a cached or freshly-acquired session cookie for the given server.</summary>
    private async Task<string?> GetSessionAsync(string serverDir, int port, CancellationToken ct)
    {
        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_sessionCache.TryGetValue(serverDir, out var cached) && DateTime.UtcNow < cached.Expiry)
                return cached.Cookie;

            var token = await LoginAsync(port, GetPassword(serverDir), ct).ConfigureAwait(false);
            if (token is not null)
                _sessionCache[serverDir] = (token, DateTime.UtcNow.AddMinutes(50));
            return token;
        }
        finally { _sessionLock.Release(); }
    }

    /// <summary>Invalidates the cached session for a server so the next call re-authenticates.</summary>
    private void InvalidateSession(string serverDir)
    {
        _sessionLock.Wait(TimeSpan.FromSeconds(1));
        try { _sessionCache.Remove(serverDir); }
        finally { _sessionLock.Release(); }
    }

    private async Task<HttpResponseMessage?> AuthGetAsync(string serverDir, string url, CancellationToken ct)
    {
        var port = GetPort(serverDir);
        if (port <= 0) return null;

        var session = await GetSessionAsync(serverDir, port, ct).ConfigureAwait(false);
        if (session is null) return null;

        using var client = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Cookie", $"wp_session={session}");
        var resp = await client.SendAsync(req, ct).ConfigureAwait(false);

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Session expired — re-authenticate once
            resp.Dispose();
            InvalidateSession(serverDir);
            session = await GetSessionAsync(serverDir, port, ct).ConfigureAwait(false);
            if (session is null) return null;
            using var retry = new HttpRequestMessage(HttpMethod.Get, url);
            retry.Headers.Add("Cookie", $"wp_session={session}");
            return await client.SendAsync(retry, ct).ConfigureAwait(false);
        }

        return resp;
    }

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

    public async Task<string?> GetSessionCookieAsync(string serverDir, CancellationToken ct = default)
    {
        var port = GetPort(serverDir);
        if (port <= 0) return null;
        return await GetSessionAsync(serverDir, port, ct).ConfigureAwait(false);
    }

    public async Task<WindrosePlusStatusResult?> GetStatusAsync(string serverDir, CancellationToken ct = default)
    {
        var port = GetPort(serverDir);
        if (port <= 0) return null;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            using var response = await AuthGetAsync(serverDir, $"http://localhost:{port}/api/status", linked.Token).ConfigureAwait(false);
            if (response is null) return null;
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

        // WindrosePlus nests server info under "server": { "name": "...", "windrose_plus": "..." }
        string? serverName = null;
        string? wpVersion = null;
        if (root.TryGetProperty("server", out var serverEl) && serverEl.ValueKind == JsonValueKind.Object)
        {
            if (serverEl.TryGetProperty("name", out var snEl) && snEl.ValueKind == JsonValueKind.String)
                serverName = snEl.GetString();
            if (serverEl.TryGetProperty("windrose_plus", out var vEl) && vEl.ValueKind == JsonValueKind.String)
                wpVersion = vEl.GetString();
        }
        // Legacy fallback
        if (serverName is null && root.TryGetProperty("serverName", out var snEl2) && snEl2.ValueKind == JsonValueKind.String)
            serverName = snEl2.GetString();
        if (wpVersion is null && root.TryGetProperty("windrosePlusVersion", out var vEl2) && vEl2.ValueKind == JsonValueKind.String)
            wpVersion = vEl2.GetString();

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
        if (port <= 0) return null;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            using var response = await AuthGetAsync(serverDir, $"http://localhost:{port}/api/livemap", linked.Token).ConfigureAwait(false);
            if (response is null) return null;
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

        // WindrosePlus uses "x"/"y" in both server_status.json and livemap_data.json
        double? worldX = null;
        foreach (var xKey in new[] { "x", "worldX" })
            if (p.TryGetProperty(xKey, out var wxEl) && wxEl.TryGetDouble(out var wx)) { worldX = wx; break; }

        double? worldY = null;
        foreach (var yKey in new[] { "y", "worldY" })
            if (p.TryGetProperty(yKey, out var wyEl) && wyEl.TryGetDouble(out var wy)) { worldY = wy; break; }

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
