using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Services;

public sealed partial class UpdateCheckService : IUpdateCheckService
{
    private const string AppId = "4129620";
    private const string ApiUrl = "https://api.steamcmd.net/v1/info/4129620";

    private static readonly Regex BuildIdRegex =
        new("\"buildid\"\\s+\"(\\d+)\"", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly IHttpClientFactory _httpFactory;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(
        IHttpClientFactory httpFactory,
        IAppSettingsService settings,
        ILogger<UpdateCheckService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckAsync(string? installDir, CancellationToken ct = default)
    {
        var dir = installDir ?? _settings.Current.ServerInstallDir;
        var installed = ReadInstalledBuildId(dir);

        string? latest = null;
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var resp = await http.GetAsync(ApiUrl, ct);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            latest = ExtractLatestBuildId(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check request failed");
            return new UpdateCheckResult(false, installed, null, "Update-Check nicht verfügbar");
        }

        if (string.IsNullOrWhiteSpace(latest))
            return new UpdateCheckResult(false, installed, null, "Update-Check nicht verfügbar");

        if (string.IsNullOrWhiteSpace(installed))
            return new UpdateCheckResult(true, null, latest, $"Server nicht installiert. Aktuelle Build-ID: {latest}");

        var hasUpdate = !string.Equals(installed, latest, StringComparison.Ordinal);
        var msg = hasUpdate
            ? $"Update verfügbar: {installed} → {latest}"
            : $"Server ist aktuell (Build {installed}).";
        return new UpdateCheckResult(hasUpdate, installed, latest, msg);
    }

    private static string? ExtractLatestBuildId(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty(AppId, out var app) &&
                app.TryGetProperty("depots", out var depots) &&
                depots.TryGetProperty("branches", out var branches) &&
                branches.TryGetProperty("public", out var pub) &&
                pub.TryGetProperty("buildid", out var build))
            {
                return build.ValueKind == JsonValueKind.String ? build.GetString() : build.ToString();
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static string? ReadInstalledBuildId(string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)) return null;
        try
        {
            var manifest = Path.Combine(installDir, "steamapps", $"appmanifest_{AppId}.acf");
            if (!File.Exists(manifest)) return null;
            foreach (var line in File.ReadLines(manifest))
            {
                var m = BuildIdRegex.Match(line);
                if (m.Success) return m.Groups[1].Value;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
