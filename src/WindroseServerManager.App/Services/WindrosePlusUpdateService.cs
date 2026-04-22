using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Vergleicht das neueste WindrosePlus-GitHub-Release mit dem <c>.wplus-version</c>-Marker
/// jedes opt-in Servers. Liefert pro Server den Status — inklusive "Update verfügbar".
/// Rein informativ, es wird nichts installiert.
/// </summary>
public sealed class WindrosePlusUpdateService : IWindrosePlusUpdateService
{
    private const string ReleaseUrl = "https://github.com/windrose-plus/windrose-plus/releases/latest";

    private readonly IWindrosePlusService _wplus;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<WindrosePlusUpdateService> _logger;

    public WindrosePlusUpdateResult? LastResult { get; private set; }

    public event Action<WindrosePlusUpdateResult>? UpdateChecked;

    public WindrosePlusUpdateService(
        IWindrosePlusService wplus,
        IAppSettingsService settings,
        ILogger<WindrosePlusUpdateService> logger)
    {
        _wplus = wplus;
        _settings = settings;
        _logger = logger;
    }

    public async Task<WindrosePlusUpdateResult> CheckAsync(CancellationToken ct = default)
    {
        var result = await CheckInternalAsync(ct).ConfigureAwait(false);
        LastResult = result;
        try { UpdateChecked?.Invoke(result); }
        catch (Exception ex) { _logger.LogWarning(ex, "UpdateChecked-Subscriber warf Exception"); }
        return result;
    }

    private async Task<WindrosePlusUpdateResult> CheckInternalAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        // Nur Server mit aktivem WindrosePlus berücksichtigen.
        var optedIn = _settings.Current.Servers
            .Where(s => _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(s.InstallDir, false))
            .ToList();

        if (optedIn.Count == 0)
        {
            return new WindrosePlusUpdateResult(
                AnyUpdate: false,
                LatestTag: null,
                ReleaseUrl: ReleaseUrl,
                Servers: Array.Empty<WindrosePlusServerStatus>(),
                Message: "Keine Server mit aktivem WindrosePlus.",
                Succeeded: true,
                CheckedUtc: nowUtc);
        }

        string? latestTag;
        try
        {
            var release = await _wplus.FetchLatestAsync(ct).ConfigureAwait(false);
            latestTag = release.Tag;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WindrosePlus-Update-Check: GitHub nicht erreichbar");
            return new WindrosePlusUpdateResult(
                AnyUpdate: false,
                LatestTag: null,
                ReleaseUrl: ReleaseUrl,
                Servers: Array.Empty<WindrosePlusServerStatus>(),
                Message: "Update-Check nicht erreichbar.",
                Succeeded: false,
                CheckedUtc: nowUtc);
        }

        var statuses = new List<WindrosePlusServerStatus>(optedIn.Count);
        foreach (var s in optedIn)
        {
            var marker = _wplus.ReadVersionMarker(s.InstallDir);
            var installed = marker?.Tag;
            var hasUpdate = !string.IsNullOrWhiteSpace(installed)
                            && !TagsEqual(installed!, latestTag);
            statuses.Add(new WindrosePlusServerStatus(
                s.Id, s.Name, s.InstallDir, installed, hasUpdate));
        }

        var anyUpdate = statuses.Any(x => x.HasUpdate);
        var msg = anyUpdate
            ? $"WindrosePlus {latestTag} verfügbar."
            : $"WindrosePlus ist aktuell ({latestTag}).";

        return new WindrosePlusUpdateResult(
            AnyUpdate: anyUpdate,
            LatestTag: latestTag,
            ReleaseUrl: ReleaseUrl,
            Servers: statuses,
            Message: msg,
            Succeeded: true,
            CheckedUtc: nowUtc);
    }

    /// <summary>Normalisierter Tag-Vergleich: führendes "v" egal, trailing-Whitespace egal.</summary>
    private static bool TagsEqual(string a, string b)
    {
        static string N(string s)
        {
            s = s.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            return s;
        }
        return string.Equals(N(a), N(b), StringComparison.OrdinalIgnoreCase);
    }
}
