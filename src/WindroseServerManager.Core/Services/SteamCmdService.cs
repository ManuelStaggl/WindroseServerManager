using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace WindroseServerManager.Core.Services;

public sealed class SteamCmdService : ISteamCmdService
{
    private const string SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    private readonly ILogger<SteamCmdService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _steamCmdDir;
    private readonly string _steamCmdExe;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public SteamCmdService(ILogger<SteamCmdService> logger, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _steamCmdDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindroseServerManager", "steamcmd");
        _steamCmdExe = Path.Combine(_steamCmdDir, "steamcmd.exe");
    }

    public async Task<string> EnsureSteamCmdAsync(IProgress<string>? log, CancellationToken ct = default)
    {
        await _ensureLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(_steamCmdExe))
            {
                log?.Report($"SteamCMD bereits installiert: {_steamCmdExe}");
                return _steamCmdExe;
            }

            Directory.CreateDirectory(_steamCmdDir);
            var zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");

            log?.Report($"Lade SteamCMD von {SteamCmdZipUrl}...");
            _logger.LogInformation("Downloading SteamCMD from {Url} to {Path}", SteamCmdZipUrl, zipPath);

            using (var http = _httpFactory.CreateClient("steam"))
            {
                http.Timeout = TimeSpan.FromMinutes(5);
                using var response = await http.GetAsync(SteamCmdZipUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            log?.Report("Entpacke SteamCMD...");
            _logger.LogInformation("Extracting SteamCMD archive");
            ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);

            try { File.Delete(zipPath); } catch (Exception ex) { _logger.LogDebug(ex, "Zip cleanup failed"); }

            if (!File.Exists(_steamCmdExe))
            {
                throw new InvalidOperationException(
                    $"SteamCMD extracted but steamcmd.exe was not found in {_steamCmdDir}");
            }

            log?.Report("Running SteamCMD self-update (may take 1-2 minutes)...");
            _logger.LogInformation("Running initial SteamCMD self-update");
            await foreach (var line in RunAsync("+quit", ct).ConfigureAwait(false))
            {
                log?.Report(line);
            }

            log?.Report($"SteamCMD bereit: {_steamCmdExe}");
            return _steamCmdExe;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    public async IAsyncEnumerable<string> RunAsync(
        string arguments,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!File.Exists(_steamCmdExe))
        {
            throw new InvalidOperationException(
                "SteamCMD nicht installiert. Zuerst EnsureSteamCmdAsync aufrufen.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _steamCmdExe,
            Arguments = arguments,
            WorkingDirectory = _steamCmdDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _logger.LogInformation("Starting steamcmd with args: {Args}", arguments);
        if (!process.Start())
        {
            throw new InvalidOperationException("SteamCMD-Prozess konnte nicht gestartet werden");
        }

        try { process.StandardInput.Close(); } catch { }

        var stdoutTask = PumpStreamAsync(process.StandardOutput, channel.Writer, ct);
        var stderrTask = PumpStreamAsync(process.StandardError, channel.Writer, ct);
        var completion = Task.Run(async () =>
        {
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false); }
            catch { }
            channel.Writer.TryComplete();
        });

        using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger.LogWarning("Cancellation requested, killing steamcmd");
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error killing steamcmd process");
            }
        });

        await foreach (var line in channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        await completion.ConfigureAwait(false);
        _logger.LogInformation("steamcmd exited with code {Code}", process.ExitCode);
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// SteamCMD schreibt Download-Progress mit CR (\r) statt LF — wir lesen char-by-char
    /// und splitten an \r ODER \n, damit Progress-Updates live im Log erscheinen.
    /// </summary>
    private static async Task PumpStreamAsync(
        StreamReader reader,
        ChannelWriter<string> writer,
        CancellationToken ct)
    {
        var buffer = new char[1024];
        var sb = new System.Text.StringBuilder(256);
        while (!ct.IsCancellationRequested)
        {
            int read;
            try { read = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false); }
            catch { break; }
            if (read <= 0) break;

            for (int i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c == '\r' || c == '\n')
                {
                    if (sb.Length > 0)
                    {
                        writer.TryWrite(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0)
        {
            writer.TryWrite(sb.ToString());
        }
    }
}
