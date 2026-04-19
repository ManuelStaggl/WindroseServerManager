using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class WindrosePlusService : IWindrosePlusService
{
    private const string WindrosePlusApiUrl = "https://api.github.com/repos/HumanGenome/WindrosePlus/releases/latest";
    private const string Ue4ssApiUrl       = "https://api.github.com/repos/UE4SS-RE/RE-UE4SS/releases/latest";
    private const string WindrosePlusAssetName = "WindrosePlus.zip";
    private const string Ue4ssAssetNamePrefix = "UE4SS_";
    private const string UserAgent = "WindroseServerManager-WindrosePlus";
    private const string HttpClientName = "github";
    private const string BundledLicenseFileName = "WindrosePlus-LICENSE.txt";
    private const string VersionMarkerFileName = ".wplus-version";

    private static readonly string[] UserOwnedRelativePaths =
    {
        "windrose_plus.json",
        "windrose_plus.ini",
        Path.Combine("WindrosePlus", "config", "windrose_plus.ini"),
    };

    private readonly ILogger<WindrosePlusService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _cacheDir;
    private readonly string _metadataCachePath;
    private readonly string _archiveCachePath;
    private readonly string _ue4ssMetadataCachePath;
    private readonly string _ue4ssArchiveCachePath;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public WindrosePlusService(
        ILogger<WindrosePlusService> logger,
        IHttpClientFactory httpFactory,
        string? cacheDir = null)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindroseServerManager", "cache", "windroseplus");
        _metadataCachePath = Path.Combine(_cacheDir, "latest.json");
        _archiveCachePath = Path.Combine(_cacheDir, "WindrosePlus.zip");
        _ue4ssMetadataCachePath = Path.Combine(_cacheDir, "ue4ss-latest.json");
        _ue4ssArchiveCachePath = Path.Combine(_cacheDir, "UE4SS.zip");
    }

    public Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct = default) =>
        FetchReleaseAsync(
            apiUrl: WindrosePlusApiUrl,
            metadataCachePath: _metadataCachePath,
            selectAsset: assets => SelectAssetByExactName(assets, WindrosePlusAssetName),
            logLabel: "WindrosePlus",
            ct: ct);

    private Task<WindrosePlusRelease> FetchUe4ssLatestAsync(CancellationToken ct) =>
        FetchReleaseAsync(
            apiUrl: Ue4ssApiUrl,
            metadataCachePath: _ue4ssMetadataCachePath,
            selectAsset: SelectUe4ssAsset,
            logLabel: "UE4SS",
            ct: ct);

    private async Task<WindrosePlusRelease> FetchReleaseAsync(
        string apiUrl,
        string metadataCachePath,
        Func<JsonElement, (string Name, string Url, long Size, string? Digest)?> selectAsset,
        string logLabel,
        CancellationToken ct)
    {
        try
        {
            using var http = _httpFactory.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await http.GetAsync(apiUrl, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var release = ParseReleaseJson(json, selectAsset, logLabel);

            // Persist raw JSON as offline fallback.
            try
            {
                Directory.CreateDirectory(_cacheDir);
                await File.WriteAllTextAsync(metadataCachePath, json, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to persist {Label} metadata cache at {Path}", logLabel, metadataCachePath);
            }

            return release;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(ex, "{Label} release metadata fetch failed; attempting cache at {Path}", logLabel, metadataCachePath);
            if (File.Exists(metadataCachePath))
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(metadataCachePath, ct).ConfigureAwait(false);
                    return ParseReleaseJson(cachedJson, selectAsset, logLabel);
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "{Label} cached metadata at {Path} is corrupt", logLabel, metadataCachePath);
                }
            }
            throw new WindrosePlusOfflineException(
                $"{logLabel} release metadata unavailable and no cached copy found.", ex);
        }
    }

    private WindrosePlusRelease ParseReleaseJson(
        string json,
        Func<JsonElement, (string Name, string Url, long Size, string? Digest)?> selectAsset,
        string logLabel)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException($"{logLabel} latest release is a draft.");
        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException($"{logLabel} latest release is a prerelease.");

        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidOperationException($"{logLabel} release has no tag_name.");

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{logLabel} release has no assets array.");

        var picked = selectAsset(assets)
            ?? throw new InvalidOperationException($"{logLabel} release has no matching asset.");

        if (string.IsNullOrWhiteSpace(picked.Digest))
        {
            _logger.LogWarning("{Label} asset {Asset} has no digest field; SHA-256 will be computed locally", logLabel, picked.Name);
        }

        return new WindrosePlusRelease(tag!, picked.Name, picked.Url, picked.Size, picked.Digest);
    }

    private static (string Name, string Url, long Size, string? Digest)? SelectAssetByExactName(JsonElement assets, string exactName)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase))
                return ReadAssetTuple(asset);
        }
        return null;
    }

    private static (string Name, string Url, long Size, string? Digest)? SelectUe4ssAsset(JsonElement assets)
    {
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null) continue;
            if (name.StartsWith(Ue4ssAssetNamePrefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return ReadAssetTuple(asset);
            }
        }
        return null;
    }

    private static (string Name, string Url, long Size, string? Digest) ReadAssetTuple(JsonElement asset)
    {
        var name = asset.GetProperty("name").GetString()!;
        var url = asset.GetProperty("browser_download_url").GetString()!;
        var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0L;
        string? digest = null;
        if (asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String)
        {
            digest = d.GetString();
        }
        return (name, url, size, digest);
    }

    public async Task<WindrosePlusInstallResult> InstallAsync(
        string serverInstallDir,
        IProgress<InstallProgress>? progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir))
            throw new ArgumentException("Server install directory must be provided.", nameof(serverInstallDir));

        await _installLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheDir);
            Directory.CreateDirectory(serverInstallDir);

            // -------- Fetch release metadata (WindrosePlus) --------
            Report(progress, InstallPhase.FetchingRelease, "Lade WindrosePlus-Release-Info...");
            WindrosePlusRelease wplusRelease;
            try
            {
                wplusRelease = await FetchLatestAsync(ct).ConfigureAwait(false);
            }
            catch (WindrosePlusOfflineException ex) when (File.Exists(_archiveCachePath))
            {
                // API offline but cached archive exists — synthesize a minimal release from the cache.
                _logger.LogWarning(ex, "WindrosePlus API offline; proceeding from cached archive at {Path}", _archiveCachePath);
                wplusRelease = new WindrosePlusRelease(
                    Tag: "cached",
                    AssetName: WindrosePlusAssetName,
                    DownloadUrl: string.Empty,
                    SizeBytes: new FileInfo(_archiveCachePath).Length,
                    DigestSha256: null);
            }

            // -------- Fetch release metadata (UE4SS) --------
            Report(progress, InstallPhase.FetchingRelease, "Lade UE4SS-Release-Info...");
            WindrosePlusRelease? ue4ssRelease = null;
            try
            {
                ue4ssRelease = await FetchUe4ssLatestAsync(ct).ConfigureAwait(false);
            }
            catch (WindrosePlusOfflineException ex)
            {
                _logger.LogWarning(ex, "UE4SS API offline and no UE4SS cache — continuing without UE4SS payload");
            }

            // -------- Download / cache archives --------
            Report(progress, InstallPhase.DownloadingArchive, "Lade WindrosePlus-Archiv...");
            var wplusZip = await EnsureArchiveCachedAsync(wplusRelease, _archiveCachePath, "WindrosePlus", ct).ConfigureAwait(false);

            string? ue4ssZip = null;
            if (ue4ssRelease is not null)
            {
                Report(progress, InstallPhase.DownloadingArchive, "Lade UE4SS-Archiv...");
                try
                {
                    ue4ssZip = await EnsureArchiveCachedAsync(ue4ssRelease, _ue4ssArchiveCachePath, "UE4SS", ct).ConfigureAwait(false);
                }
                catch (WindrosePlusOfflineException ex)
                {
                    _logger.LogWarning(ex, "UE4SS archive unavailable — continuing without UE4SS payload");
                    ue4ssZip = null;
                    ue4ssRelease = null;
                }
            }

            // -------- Verify digests --------
            Report(progress, InstallPhase.VerifyingDigest, "Verifiziere SHA-256 (WindrosePlus)...");
            var wplusSha = await VerifyDigestAsync(wplusZip, wplusRelease.DigestSha256, ct).ConfigureAwait(false);

            if (ue4ssZip is not null && ue4ssRelease is not null)
            {
                Report(progress, InstallPhase.VerifyingDigest, "Verifiziere SHA-256 (UE4SS)...");
                _ = await VerifyDigestAsync(ue4ssZip, ue4ssRelease.DigestSha256, ct).ConfigureAwait(false);
            }

            // -------- Extract + atomic merge on same volume --------
            var serverDirFull = Path.GetFullPath(serverInstallDir);
            var parentDir = Path.GetDirectoryName(serverDirFull);
            if (string.IsNullOrEmpty(parentDir))
                throw new InvalidOperationException($"Cannot resolve parent of {serverDirFull}.");

            var tempRoot = Path.Combine(parentDir, ".wplus-install-temp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                Report(progress, InstallPhase.Extracting, "Entpacke WindrosePlus...");
                ZipFile.ExtractToDirectory(wplusZip, tempRoot, overwriteFiles: true);

                if (ue4ssZip is not null)
                {
                    Report(progress, InstallPhase.Extracting, "Entpacke UE4SS...");
                    var ue4ssTarget = Path.Combine(tempRoot, "R5", "Binaries", "Win64");
                    Directory.CreateDirectory(ue4ssTarget);
                    ZipFile.ExtractToDirectory(ue4ssZip, ue4ssTarget, overwriteFiles: true);
                }

                Report(progress, InstallPhase.Installing, "Kopiere LICENSE...");
                CopyLicense(tempRoot, serverDirFull);

                Report(progress, InstallPhase.Installing, "Installiere Dateien...");
                AtomicMergeIntoServer(tempRoot, serverDirFull, preserveUserConfig: true);

                Report(progress, InstallPhase.WritingMarker, "Schreibe Versionsmarker...");
                var installedUtc = DateTime.UtcNow;
                var marker = new WindrosePlusVersionMarker(
                    Tag: wplusRelease.Tag,
                    InstalledUtc: installedUtc,
                    ArchiveSha256: wplusSha,
                    Ue4ssTag: ue4ssRelease?.Tag);
                var markerJson = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(serverDirFull, VersionMarkerFileName), markerJson, ct).ConfigureAwait(false);

                Report(progress, InstallPhase.Complete, "WindrosePlus installiert.");
                _logger.LogInformation("WindrosePlus {Tag} installed into {Dir} (UE4SS: {Ue4ssTag})",
                    wplusRelease.Tag, serverDirFull, ue4ssRelease?.Tag ?? "<none>");

                return new WindrosePlusInstallResult(
                    Tag: wplusRelease.Tag,
                    ServerInstallDir: serverDirFull,
                    InstalledUtc: installedUtc,
                    ArchiveSha256: wplusSha,
                    Ue4ssTag: ue4ssRelease?.Tag);
            }
            finally
            {
                Report(progress, InstallPhase.CleaningUp, "Räume temporäres Verzeichnis auf...");
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); }
                catch (Exception ex) { _logger.LogDebug(ex, "Temp dir cleanup failed at {Path}", tempRoot); }
            }
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task<string> EnsureArchiveCachedAsync(
        WindrosePlusRelease release,
        string cachePath,
        string logLabel,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_cacheDir);

        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            // Synthetic "cached" release — must have the archive on disk already.
            if (File.Exists(cachePath))
            {
                _logger.LogInformation("{Label} using cached archive at {Path} (no download URL)", logLabel, cachePath);
                return cachePath;
            }
            throw new WindrosePlusOfflineException(
                $"{logLabel} archive unavailable: no download URL and no cached copy at {cachePath}.");
        }

        var tmpPath = cachePath + ".tmp";
        try
        {
            using var http = _httpFactory.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromMinutes(5);
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));

            using var resp = await http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using (var fs = File.Create(tmpPath))
            {
                await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            File.Move(tmpPath, cachePath, overwrite: true);
            _logger.LogInformation("{Label} downloaded {Tag} ({Size} bytes) to {Path}",
                logLabel, release.Tag, release.SizeBytes, cachePath);
            return cachePath;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* best effort */ }

            if (File.Exists(cachePath))
            {
                _logger.LogWarning(ex, "{Label} download failed; using cached archive at {Path}", logLabel, cachePath);
                return cachePath;
            }

            throw new WindrosePlusOfflineException(
                $"{logLabel} archive download failed and no cached copy exists at {cachePath}.", ex);
        }
    }

    private async Task<string> VerifyDigestAsync(string path, string? expectedDigest, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var actualBytes = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        var actualHex = Convert.ToHexString(actualBytes);

        if (string.IsNullOrWhiteSpace(expectedDigest))
        {
            _logger.LogWarning("No publisher digest provided for {Path}; recording computed SHA-256 {Sha}", path, actualHex);
            return actualHex;
        }

        var prefix = "sha256:";
        var expectedHex = expectedDigest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? expectedDigest[prefix.Length..]
            : expectedDigest;

        if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new WindrosePlusDigestMismatchException(
                $"Archive SHA-256 mismatch at {path}: expected {expectedHex}, actual {actualHex}.");
        }

        return actualHex;
    }

    private void AtomicMergeIntoServer(string tempRoot, string serverDir, bool preserveUserConfig)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(tempRoot, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(tempRoot, sourceFile);
            var dest = Path.Combine(serverDir, rel);

            if (preserveUserConfig
                && UserOwnedRelativePaths.Any(u => rel.Equals(u, StringComparison.OrdinalIgnoreCase))
                && File.Exists(dest))
            {
                _logger.LogInformation("Preserving user config {Rel}", rel);
                continue;
            }

            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            File.Move(sourceFile, dest, overwrite: true);
        }
    }

    private void CopyLicense(string tempRoot, string serverDir)
    {
        string? sourceLicense = null;
        foreach (var candidate in new[] { "LICENSE", "LICENSE.txt", "license", "license.txt" })
        {
            var candidatePath = Path.Combine(tempRoot, candidate);
            if (File.Exists(candidatePath))
            {
                sourceLicense = candidatePath;
                break;
            }
        }

        if (sourceLicense is null)
        {
            _logger.LogWarning("WindrosePlus archive contained no LICENSE file at {Root}", tempRoot);
            return;
        }

        var destPath = Path.Combine(serverDir, BundledLicenseFileName);
        File.Copy(sourceLicense, destPath, overwrite: true);
    }

    public (string ExePath, string ExtraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir))
            throw new ArgumentException("Server install directory must be provided.", nameof(serverInstallDir));

        var serverDirFull = Path.GetFullPath(serverInstallDir);

        if (!info.WindrosePlusActive)
        {
            var exe = ServerInstallService.FindServerBinary(serverDirFull)
                ?? throw new FileNotFoundException("WindroseServer binary not found.", serverDirFull);
            return (Path.GetFullPath(exe), string.Empty);
        }

        var bat = Path.Combine(serverDirFull, "StartWindrosePlusServer.bat");
        if (File.Exists(bat))
        {
            return (Path.GetFullPath(bat), string.Empty);
        }

        _logger.LogWarning(
            "WindrosePlus flagged active but StartWindrosePlusServer.bat missing at {Path} — falling back to WindroseServer.exe",
            bat);

        var fallback = ServerInstallService.FindServerBinary(serverDirFull)
            ?? throw new FileNotFoundException(
                "Neither StartWindrosePlusServer.bat nor WindroseServer binary found.", serverDirFull);
        return (Path.GetFullPath(fallback), string.Empty);
    }

    public WindrosePlusVersionMarker? ReadVersionMarker(string serverInstallDir)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir))
            return null;

        var path = Path.Combine(serverInstallDir, VersionMarkerFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WindrosePlusVersionMarker>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse {File}", path);
            return null;
        }
    }

    private static void Report(IProgress<InstallProgress>? progress, InstallPhase phase, string message)
    {
        progress?.Report(new InstallProgress(phase, message, null, null));
    }
}
