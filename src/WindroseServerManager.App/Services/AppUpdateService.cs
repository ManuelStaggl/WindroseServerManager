using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Fragt die GitHub Releases API nach dem neuesten Release und vergleicht mit der laufenden Assembly-Version.
/// Kein Auth — GitHub erlaubt 60 Requests/h pro IP unauthentifiziert, völlig ausreichend für einen Desktop-Client.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/ManuelStaggl/WindroseServerManager/releases/latest";
    private const string UserAgent = "WindroseServerManager-UpdateCheck";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AppUpdateService> _logger;

    public event Action<AppUpdateResult>? UpdateChecked;

    public AppUpdateService(IHttpClientFactory httpFactory, ILogger<AppUpdateService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<AppUpdateResult> CheckAsync(CancellationToken ct = default)
    {
        var result = await CheckInternalAsync(ct).ConfigureAwait(false);
        try { UpdateChecked?.Invoke(result); }
        catch (Exception ex) { _logger.LogWarning(ex, "UpdateChecked-Subscriber warf Exception"); }
        return result;
    }

    private async Task<AppUpdateResult> CheckInternalAsync(CancellationToken ct)
    {
        var current = GetCurrentVersion();
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, current.ToString()));
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await http.GetAsync(ApiUrl, ct).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("GitHub releases endpoint returned 404 (noch kein Release veröffentlicht)");
                return new AppUpdateResult(false, current.ToString(), null, null, null,
                    "Noch kein Release veröffentlicht.");
            }
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;
            var isDraft = root.TryGetProperty("draft", out var draftEl) && draftEl.GetBoolean();
            var isPrerelease = root.TryGetProperty("prerelease", out var preEl) && preEl.GetBoolean();

            if (isDraft || isPrerelease)
            {
                _logger.LogDebug("Skipping draft/prerelease {Tag}", tag);
                return new AppUpdateResult(false, current.ToString(), null, null, null,
                    "Keine stabile Version verfügbar.");
            }

            if (string.IsNullOrWhiteSpace(tag) || !TryParseVersion(tag, out var latest))
            {
                _logger.LogWarning("Konnte Tag nicht parsen: {Tag}", tag);
                return new AppUpdateResult(false, current.ToString(), null, null, null,
                    "Release-Version konnte nicht gelesen werden.");
            }

            var latestStr = NormalizeVersion(latest);
            var downloadUrl = ExtractInstallerAsset(root);
            var hasUpdate = latest > current;

            var msg = hasUpdate
                ? $"Update verfügbar: v{latestStr}"
                : $"Du verwendest die aktuelle Version (v{current}).";

            return new AppUpdateResult(hasUpdate, current.ToString(), latestStr, htmlUrl, downloadUrl, msg);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App-Update-Check fehlgeschlagen");
            return new AppUpdateResult(false, current.ToString(), null, null, null,
                "Update-Check nicht erreichbar.");
        }
    }

    private static Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info) && TryParseVersion(info, out var parsed))
            return parsed;

        return asm.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private static bool TryParseVersion(string raw, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);

        // SemVer-Suffix wie "1.0.1-beta.1+build" abschneiden — nur numerischer Kern.
        var cutoff = s.IndexOfAny(new[] { '-', '+' });
        if (cutoff > 0) s = s.Substring(0, cutoff);

        return Version.TryParse(s, out version!);
    }

    private static string NormalizeVersion(Version v)
    {
        // Trailing ".0" wegkürzen, damit "1.0.1.0" als "1.0.1" angezeigt wird.
        if (v.Revision > 0) return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        if (v.Build > 0) return $"{v.Major}.{v.Minor}.{v.Build}";
        return $"{v.Major}.{v.Minor}";
    }

    private static string? ExtractInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        string? anyDownload = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(url)) continue;

            anyDownload ??= url;

            // Installer-Asset bevorzugen (Setup*.exe).
            if (!string.IsNullOrWhiteSpace(name) &&
                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("setup", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }
        return anyDownload;
    }
}
