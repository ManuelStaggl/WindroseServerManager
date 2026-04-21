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
    private const string WindrosePlusAssetName = "WindrosePlus.zip";
    private const string UserAgent = "WindroseServerManager-WindrosePlus";
    private const string HttpClientName = "github";
    private const string VersionMarkerFileName = ".wplus-version";

    private readonly ILogger<WindrosePlusService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAppSettingsService _settings;
    private readonly string _cacheDir;
    private readonly string _metadataCachePath;
    private readonly string _archiveCachePath;
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private readonly Dictionary<string, System.Diagnostics.Process> _dashboardProcesses = new();

    public WindrosePlusService(
        ILogger<WindrosePlusService> logger,
        IHttpClientFactory httpFactory,
        IAppSettingsService settings,
        string? cacheDir = null)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _settings = settings;
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindroseServerManager", "cache", "windroseplus");
        _metadataCachePath = Path.Combine(_cacheDir, "latest.json");
        _archiveCachePath = Path.Combine(_cacheDir, "WindrosePlus.zip");
    }

    public Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct = default) =>
        FetchReleaseAsync(
            apiUrl: "https://api.github.com/repos/HumanGenome/WindrosePlus/releases/latest",
            metadataCachePath: _metadataCachePath,
            selectAsset: assets => SelectAssetByExactName(assets, WindrosePlusAssetName),
            logLabel: "WindrosePlus",
            ct: ct);

    private async Task<WindrosePlusRelease> FetchReleaseAsync(
        string apiUrl,
        string metadataCachePath,
        Func<JsonElement, (string Name, string Url, long Size, string? Digest)?> selectAsset,
        string logLabel,
        CancellationToken ct = default)
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

            // -------- Fetch + download WindrosePlus release --------
            Report(progress, InstallPhase.FetchingRelease, "Lade WindrosePlus-Release-Info...");
            WindrosePlusRelease wplusRelease;
            try
            {
                wplusRelease = await FetchLatestAsync(ct).ConfigureAwait(false);
            }
            catch (WindrosePlusOfflineException ex) when (File.Exists(_archiveCachePath))
            {
                _logger.LogWarning(ex, "WindrosePlus API offline; proceeding from cached archive at {Path}", _archiveCachePath);
                wplusRelease = new WindrosePlusRelease(
                    Tag: "cached",
                    AssetName: WindrosePlusAssetName,
                    DownloadUrl: string.Empty,
                    SizeBytes: new FileInfo(_archiveCachePath).Length,
                    DigestSha256: null);
            }

            Report(progress, InstallPhase.DownloadingArchive, "Lade WindrosePlus-Archiv...");
            var wplusZip = await EnsureArchiveCachedAsync(wplusRelease, _archiveCachePath, "WindrosePlus", ct).ConfigureAwait(false);

            Report(progress, InstallPhase.VerifyingDigest, "Verifiziere SHA-256...");
            var wplusSha = await VerifyDigestAsync(wplusZip, wplusRelease.DigestSha256, ct).ConfigureAwait(false);

            // -------- Extract ZIP to temp, then run official install.ps1 --------
            var serverDirFull = Path.GetFullPath(serverInstallDir);
            var parentDir = Path.GetDirectoryName(serverDirFull)
                ?? throw new InvalidOperationException($"Cannot resolve parent of {serverDirFull}.");

            var tempRoot = Path.Combine(parentDir, ".wplus-install-temp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                Report(progress, InstallPhase.Extracting, "Entpacke WindrosePlus...");
                ZipFile.ExtractToDirectory(wplusZip, tempRoot, overwriteFiles: true);

                Report(progress, InstallPhase.Installing, "Führe install.ps1 aus...");
                await RunInstallScriptAsync(tempRoot, serverDirFull, progress, ct).ConfigureAwait(false);

                Report(progress, InstallPhase.WritingMarker, "Schreibe Versionsmarker...");
                var installedUtc = DateTime.UtcNow;
                var marker = new WindrosePlusVersionMarker(
                    Tag: wplusRelease.Tag,
                    InstalledUtc: installedUtc,
                    ArchiveSha256: wplusSha,
                    Ue4ssTag: null);
                var markerJson = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(serverDirFull, VersionMarkerFileName), markerJson, ct).ConfigureAwait(false);

                Report(progress, InstallPhase.Complete, "WindrosePlus installiert.");
                _logger.LogInformation("WindrosePlus {Tag} installed into {Dir}", wplusRelease.Tag, serverDirFull);

                return new WindrosePlusInstallResult(
                    Tag: wplusRelease.Tag,
                    ServerInstallDir: serverDirFull,
                    InstalledUtc: installedUtc,
                    ArchiveSha256: wplusSha,
                    Ue4ssTag: null);
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

    private async Task RunInstallScriptAsync(string tempRoot, string serverDirFull, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(tempRoot, "install.ps1")))
            throw new InvalidOperationException($"install.ps1 not found in WindrosePlus archive at {tempRoot}.");

        // install.ps1 uses $scriptDir = $gameDir — it expects its companion folders
        // (WindrosePlus/, server/, tools/) to live inside the server directory, not in the temp dir.
        // Strategy: copy everything from tempRoot into the server dir, run install.ps1 from there
        // without -GameDir (so $PSScriptRoot resolves to the server dir), then clean up.
        CopyDirectoryContents(tempRoot, serverDirFull);

        var scriptInGameDir = Path.Combine(serverDirFull, "install.ps1");
        var serverDirArg = serverDirFull.TrimEnd('\\', '/');

        // Prefer pwsh (PowerShell 7+), fall back to powershell (Windows PowerShell 5)
        var ps = File.Exists(@"C:\Program Files\PowerShell\7\pwsh.exe") ? "pwsh" : "powershell";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ps,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptInGameDir}\"",
            WorkingDirectory = serverDirFull,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = new System.Diagnostics.Process { StartInfo = psi };
        proc.Start();

        // Stream output as progress messages
        var readTask = Task.Run(async () =>
        {
            while (await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogInformation("install.ps1: {Line}", line);
                    Report(progress, InstallPhase.Installing, line.Trim());
                }
            }
        }, ct);

        var errTask = proc.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(readTask, errTask).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        var stderr = await errTask.ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("install.ps1 stderr: {Err}", stderr.Trim());

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"install.ps1 exited with code {proc.ExitCode}. Check the log for details.");

        _logger.LogInformation("install.ps1 completed successfully for {Dir}", serverDirFull);

        // Clean up files that were staged into the server dir for the script (they've been
        // processed into windrose_plus/ by install.ps1 and are no longer needed at the root).
        foreach (var name in new[] { "install.ps1", "WindrosePlus", "server", "tools", "README.md", "LICENSE", "UE4SS-settings.ini" })
        {
            var path = Path.Combine(serverDirFull, name);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Cleanup of staged file {Name} failed (non-fatal)", name); }
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var target = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryContents(dir, target);
        }
    }

    public (string ExePath, string ExtraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir))
            throw new ArgumentException("Server install directory must be provided.", nameof(serverInstallDir));

        var serverDirFull = Path.GetFullPath(serverInstallDir);
        var exe = ServerInstallService.FindServerBinary(serverDirFull)
            ?? throw new FileNotFoundException("WindroseServer binary not found.", serverDirFull);
        return (Path.GetFullPath(exe), string.Empty);
    }

    public async Task RunPreLaunchAsync(string serverInstallDir, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir)) return;
        var serverDirFull = Path.GetFullPath(serverInstallDir).TrimEnd('\\', '/');

        var active = _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverDirFull, false)
                  || _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverDirFull + "\\", false)
                  || _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverInstallDir, false);
        if (!active) return;

        // Try both install layouts (our C# installer puts tools\ at root; install.ps1 puts it under windrose_plus\tools\)
        var buildPak = new[]
        {
            Path.Combine(serverDirFull, "tools", "WindrosePlus-BuildPak.ps1"),
            Path.Combine(serverDirFull, "windrose_plus", "tools", "WindrosePlus-BuildPak.ps1"),
        }.FirstOrDefault(File.Exists);

        if (buildPak is null)
        {
            _logger.LogWarning("WindrosePlus active but BuildPak script not found in {Dir} — skipping pre-launch build", serverDirFull);
            return;
        }

        _logger.LogInformation("Running WindrosePlus pre-launch BuildPak at {Script}", buildPak);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{buildPak}\" -ServerDir \"{serverDirFull}\" -RemoveStalePak",
            WorkingDirectory = serverDirFull,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = new System.Diagnostics.Process { StartInfo = psi };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdout))
            _logger.LogInformation("BuildPak output: {Output}", stdout.Trim());
        if (!string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("BuildPak stderr: {Err}", stderr.Trim());
        if (proc.ExitCode != 0)
            _logger.LogWarning("BuildPak exited with code {Code} — server will launch anyway", proc.ExitCode);
    }

    /// <summary>Creates (or overwrites) StartWindrosePlusServer.bat in the server root.
    /// This wrapper runs WindrosePlus-BuildPak.ps1 to apply config overrides before launching the server.</summary>
    public static void WriteStartBat(string serverDirFull)
    {
        const string batContent =
            "@echo off\r\n" +
            "setlocal\r\n\r\n" +
            "set \"GAMEDIR=%~dp0\"\r\n" +
            "if \"%GAMEDIR:~-1%\"==\"\\\" set \"GAMEDIR=%GAMEDIR:~0,-1%\"\r\n\r\n" +
            "set \"WP_BUILD=%GAMEDIR%\\tools\\WindrosePlus-BuildPak.ps1\"\r\n" +
            "if not exist \"%WP_BUILD%\" set \"WP_BUILD=%GAMEDIR%\\windrose_plus\\tools\\WindrosePlus-BuildPak.ps1\"\r\n\r\n" +
            "if not exist \"%WP_BUILD%\" (\r\n" +
            "    echo [WindrosePlus] Build script not found. Reinstall WindrosePlus via the app.\r\n" +
            "    if not \"%WP_NOPAUSE%\"==\"1\" pause\r\n" +
            "    exit /b 1\r\n" +
            ")\r\n\r\n" +
            "echo [WindrosePlus] Checking config overrides...\r\n" +
            "where pwsh >nul 2>&1\r\n" +
            "if %ERRORLEVEL%==0 (\r\n" +
            "    pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass ^\r\n" +
            "      -File \"%WP_BUILD%\" ^\r\n" +
            "      -ServerDir \"%GAMEDIR%\" ^\r\n" +
            "      -RemoveStalePak\r\n" +
            ") else (\r\n" +
            "    powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass ^\r\n" +
            "      -File \"%WP_BUILD%\" ^\r\n" +
            "      -ServerDir \"%GAMEDIR%\" ^\r\n" +
            "      -RemoveStalePak\r\n" +
            ")\r\n" +
            "set \"BUILD_EXIT=%ERRORLEVEL%\"\r\n\r\n" +
            "if not \"%BUILD_EXIT%\"==\"0\" (\r\n" +
            "    echo.\r\n" +
            "    echo [WindrosePlus] Config build failed (exit %BUILD_EXIT%^).\r\n" +
            "    echo Not launching server. Fix the error above and try again.\r\n" +
            "    if not \"%WP_NOPAUSE%\"==\"1\" pause\r\n" +
            "    exit /b %BUILD_EXIT%\r\n" +
            ")\r\n\r\n" +
            "echo.\r\n" +
            "echo [WindrosePlus] Starting Windrose server...\r\n" +
            "pushd \"%GAMEDIR%\"\r\n" +
            "\"%GAMEDIR%\\WindroseServer.exe\" %*\r\n" +
            "set \"SERVER_EXIT=%ERRORLEVEL%\"\r\n" +
            "popd\r\n\r\n" +
            "endlocal & exit /b %SERVER_EXIT%\r\n";

        var batPath = Path.Combine(serverDirFull, "StartWindrosePlusServer.bat");
        File.WriteAllText(batPath, batContent, System.Text.Encoding.ASCII);
    }

    public async Task StartDashboardAsync(string serverInstallDir, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverInstallDir)) return;
        var serverDirFull = Path.GetFullPath(serverInstallDir).TrimEnd('\\', '/');

        var active = _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverDirFull, false)
                  || _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverDirFull + "\\", false)
                  || _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(serverInstallDir, false);
        if (!active) return;

        var scriptPath = Path.Combine(serverDirFull, "windrose_plus", "server", "windrose_plus_server.ps1");
        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("WindrosePlus dashboard script not found at {Path} — skipping dashboard start", scriptPath);
            return;
        }

        // Stop any existing dashboard for this dir
        StopDashboard(serverInstallDir);

        var ps = File.Exists(@"C:\Program Files\PowerShell\7\pwsh.exe") ? "pwsh" : "powershell";
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ps,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\" -GameDir \"{serverDirFull}\"",
            WorkingDirectory = serverDirFull,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        _logger.LogInformation("Starting WindrosePlus dashboard server for {Dir}", serverDirFull);
        var proc = new System.Diagnostics.Process { StartInfo = psi };
        proc.Start();
        _dashboardProcesses[serverDirFull] = proc;

        await Task.CompletedTask;
    }

    public void StopDashboard(string serverInstallDir)
    {
        var serverDirFull = Path.GetFullPath(serverInstallDir).TrimEnd('\\', '/');
        if (_dashboardProcesses.TryGetValue(serverDirFull, out var proc))
        {
            _dashboardProcesses.Remove(serverDirFull);
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    _logger.LogInformation("WindrosePlus dashboard server stopped for {Dir}", serverDirFull);
                }
                proc.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping WindrosePlus dashboard server");
            }
        }
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

    public void Dispose()
    {
        foreach (var (dir, proc) in _dashboardProcesses)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
                proc.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing dashboard process for {Dir}", dir);
            }
        }
        _dashboardProcesses.Clear();
        _installLock.Dispose();
    }

    private static void Report(IProgress<InstallProgress>? progress, InstallPhase phase, string message)
    {
        progress?.Report(new InstallProgress(phase, message, null, null));
    }
}
