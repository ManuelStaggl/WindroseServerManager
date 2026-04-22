using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed partial class ServerInstallService : IServerInstallService
{
    public const string DefaultAppId = "4129620";

    private static readonly Regex ProgressRegex = ProgressRegexBuilder();
    private static readonly Regex BuildIdRegex = BuildIdRegexBuilder();
    private static readonly Regex ErrorRegex = ErrorRegexBuilder();

    private readonly ILogger<ServerInstallService> _logger;
    private readonly ISteamCmdService _steamCmd;
    private readonly IAppSettingsService _settings;

    public ServerInstallService(
        ILogger<ServerInstallService> logger,
        ISteamCmdService steamCmd,
        IAppSettingsService settings)
    {
        _logger = logger;
        _steamCmd = steamCmd;
        _settings = settings;
    }

    private string AppId =>
        string.IsNullOrWhiteSpace(_settings.Current.SteamAppId)
            ? DefaultAppId
            : _settings.Current.SteamAppId.Trim();

    public string? ValidateInstallDir(string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir))
            return "Install path cannot be empty.";

        // Trailing Whitespace/Punkt bricht Windows-Pfad-Normalisierung (Path.GetFullPath strippt das,
        // aber der User hätte keine Chance zu sehen dass sein Eingabepfad tatsächlich ein anderer ist).
        if (installDir.EndsWith(' ') || installDir.EndsWith('.'))
            return "Path cannot end with a space or period.";

        // UNC-Pfade (\\Server\Share) sind für SteamCMD/Unreal nicht verlässlich.
        if (installDir.StartsWith(@"\\", StringComparison.Ordinal))
            return "Network paths (UNC) are not supported. Please pick a local path.";

        // Non-ASCII-Zeichen (Umlaute, etc.) sind ein historischer Bruchpunkt bei SteamCMD.
        foreach (var ch in installDir)
        {
            if (ch > 127)
                return "Path contains non-ASCII characters (e.g. umlauts). SteamCMD and the Unreal server require pure ASCII paths.";
        }

        try
        {
            var full = Path.GetFullPath(installDir);

            // Pfadlänge > 260 kann mit älteren APIs Probleme machen.
            if (full.Length > 240)
                return "Path is too long (max 240 characters). Please pick a shorter path.";

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFiles) &&
                full.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
                return "Path cannot be under 'Program Files' (permission issues).";
            if (!string.IsNullOrEmpty(programFilesX86) &&
                full.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
                return "Path cannot be under 'Program Files (x86)'.";

            // System-/Windows-Ordner ebenfalls blockieren.
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows) &&
                full.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
                return "Path cannot be inside the Windows system folder.";
        }
        catch (Exception ex)
        {
            return $"Invalid path: {ex.Message}";
        }

        return null;
    }

    public Task<ServerInstallInfo> GetInstallInfoAsync(string installDir, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return Task.FromResult(ServerInstallInfo.NotInstalled(installDir ?? string.Empty));

        // Windrose Dedicated Server Binary: WindroseServer.exe oder WindowsServer.exe
        // (laut offizieller Community-Dokumentation, kann je nach Build variieren)
        var binary = FindServerBinary(installDir);
        if (binary is null)
            return Task.FromResult(ServerInstallInfo.NotInstalled(installDir));

        string? buildId = null;
        DateTime? lastUpdatedUtc = null;
        long size = 0;

        try
        {
            var manifest = Path.Combine(installDir, "steamapps", $"appmanifest_{AppId}.acf");
            if (File.Exists(manifest))
            {
                foreach (var line in File.ReadLines(manifest))
                {
                    var m = BuildIdRegex.Match(line);
                    if (m.Success)
                    {
                        buildId = m.Groups[1].Value;
                        break;
                    }
                }
                lastUpdatedUtc = File.GetLastWriteTimeUtc(manifest);
            }

            size = new FileInfo(binary).Length;
            var di = new DirectoryInfo(installDir);
            if (di.Exists)
            {
                size = di.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(fi => fi.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read full install info for {Dir}", installDir);
        }

        return Task.FromResult(new ServerInstallInfo(true, installDir, buildId, size, lastUpdatedUtc));
    }

    public async IAsyncEnumerable<InstallProgress> InstallOrUpdateAsync(
        string installDir,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var validation = ValidateInstallDir(installDir);
        if (validation is not null)
        {
            yield return new InstallProgress(InstallPhase.Failed, validation, null, null);
            yield break;
        }

        Directory.CreateDirectory(installDir);

        yield return new InstallProgress(InstallPhase.Preparing, "Bereite Installation vor...", null, null);

        yield return new InstallProgress(
            InstallPhase.DownloadingSteamCmd, "Stelle SteamCMD bereit...", null, null);

        var logBuffer = new List<string>();
        var progress = new Progress<string>();
        progress.ProgressChanged += (_, line) => logBuffer.Add(line);

        string? steamCmdPath = null;
        InstallProgress? bootstrapError = null;
        try
        {
            steamCmdPath = await _steamCmd.EnsureSteamCmdAsync(progress, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            bootstrapError = new InstallProgress(InstallPhase.Failed, "Abgebrochen.", null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bootstrap SteamCMD");
            bootstrapError = new InstallProgress(InstallPhase.Failed,
                $"SteamCMD-Setup fehlgeschlagen: {ex.Message}", null, null);
        }

        foreach (var line in logBuffer)
            yield return new InstallProgress(InstallPhase.DownloadingSteamCmd, string.Empty, null, line);

        if (bootstrapError is not null)
        {
            yield return bootstrapError;
            yield break;
        }

        _logger.LogInformation("Using SteamCMD at {Path}", steamCmdPath);

        var login = string.IsNullOrWhiteSpace(_settings.Current.SteamLogin)
            ? "anonymous"
            : _settings.Current.SteamLogin.Trim();
        // Trailing Backslash entfernen — sonst escaped er in "..." das schließende Quote
        // und SteamCMD bekommt eine kaputte Kommandozeile ("Missing configuration")
        var cleanDir = installDir.TrimEnd('\\', '/');
        var args = $"+force_install_dir \"{cleanDir}\" +login {login} +app_update {AppId} validate +quit";
        yield return new InstallProgress(
            InstallPhase.RunningSteamCmd,
            "Starte Windrose Server-Installation...",
            null,
            $"> steamcmd {args}");

        double? lastPercent = null;
        var phase = InstallPhase.DownloadingServer;

        IAsyncEnumerable<string>? lines = null;
        InstallProgress? startError = null;
        try
        {
            lines = _steamCmd.RunAsync(args, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start steamcmd");
            startError = new InstallProgress(InstallPhase.Failed, $"Fehler beim Start: {ex.Message}", null, null);
        }

        if (startError is not null)
        {
            yield return startError;
            yield break;
        }

        var enumerator = lines!.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasNext;
                string? line = null;
                InstallProgress? loopError = null;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    if (hasNext) line = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    hasNext = false;
                    loopError = new InstallProgress(InstallPhase.Failed, "Abgebrochen.", null, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SteamCMD stream error");
                    hasNext = false;
                    loopError = new InstallProgress(InstallPhase.Failed, $"Fehler: {ex.Message}", null, null);
                }

                if (loopError is not null)
                {
                    yield return loopError;
                    yield break;
                }

                if (!hasNext) break;
                if (line is null) continue;

                if (ErrorRegex.IsMatch(line))
                {
                    _logger.LogWarning("SteamCMD error line: {Line}", line);
                    yield return new InstallProgress(phase, string.Empty, lastPercent, line);
                    yield return new InstallProgress(InstallPhase.Failed,
                        $"SteamCMD meldet Fehler: {line.Trim()}", null, null);
                    yield break;
                }

                var match = ProgressRegex.Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var pct))
                {
                    lastPercent = pct / 100.0;
                    phase = InstallPhase.DownloadingServer;
                }
                else if (line.Contains("validating", StringComparison.OrdinalIgnoreCase))
                {
                    phase = InstallPhase.Validating;
                }
                else if (line.Contains("Success!", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("fully installed", StringComparison.OrdinalIgnoreCase))
                {
                    phase = InstallPhase.Complete;
                }

                yield return new InstallProgress(phase, string.Empty, lastPercent, line);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        yield return new InstallProgress(InstallPhase.Complete,
            "Installation abgeschlossen.", 1.0, null);
    }

    /// <summary>
    /// Sucht die Server-Binary: WindroseServer.exe, WindowsServer.exe (legacy) oder
    /// Windrose-Win64-Shipping.exe im Install-Dir — im Root oder in Unterordnern.
    /// </summary>
    public static string? FindServerBinary(string installDir)
    {
        string[] candidates = { "WindroseServer.exe", "WindroseServer-Win64-Shipping.exe", "WindowsServer.exe" };
        foreach (var name in candidates)
        {
            var direct = Path.Combine(installDir, name);
            if (File.Exists(direct)) return direct;
        }
        try
        {
            foreach (var name in candidates)
            {
                var hit = Directory.EnumerateFiles(installDir, name, SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null) return hit;
            }
        }
        catch { }
        return null;
    }

    [GeneratedRegex(@"progress:\s*([\d.]+)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ProgressRegexBuilder();

    [GeneratedRegex(@"""buildid""\s*""(\d+)""", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BuildIdRegexBuilder();

    [GeneratedRegex(@"\b(ERROR!|FAILED|Login Failure|Invalid Password|Missing configuration|Invalid AppID)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ErrorRegexBuilder();
}
