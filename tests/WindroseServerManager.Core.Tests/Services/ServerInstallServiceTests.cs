using System.Runtime.CompilerServices;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using WindroseServerManager.Core.Tests.TestDoubles;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

public sealed class ServerInstallServiceTests
{
    [Fact]
    public async Task InstallOrUpdateAsync_DoesNotFailOnTransientFailedLine()
    {
        var installDir = CreateTempInstallDir();
        try
        {
            var manifest = CreateManifest(installDir);
            var steamCmd = new FakeSteamCmdService(
                "Update state (0x3) reconfiguring, progress: 10.00",
                "FAILED (No Connection), retrying...",
                "Update state (0x61) downloading, progress: 100.00",
                "Success! App '4129620' fully installed.");
            var sut = CreateService(steamCmd);

            var progress = await CollectAsync(sut.InstallOrUpdateAsync(installDir));

            Assert.DoesNotContain(progress, p => p.Phase == InstallPhase.Failed);
            Assert.Equal(InstallPhase.Complete, progress[^1].Phase);
            Assert.False(File.Exists(manifest));
            Assert.Contains(steamCmd.RunArguments, args => args.Contains("+app_update 4129620 validate"));
        }
        finally
        {
            DeleteTempDir(installDir);
        }
    }

    [Fact]
    public async Task InstallOrUpdateAsync_FailsWhenLineStartsWithError()
    {
        var installDir = CreateTempInstallDir();
        try
        {
            var steamCmd = new FakeSteamCmdService("ERROR! Failed to install app '4129620' (Invalid AppID)");
            var sut = CreateService(steamCmd);

            var progress = await CollectAsync(sut.InstallOrUpdateAsync(installDir));

            Assert.Contains(progress, p => p.Phase == InstallPhase.Failed);
        }
        finally
        {
            DeleteTempDir(installDir);
        }
    }

    [Fact]
    public async Task IsUpdateAvailableAsync_ReturnsTrueWhenSteamReportsOutOfDate()
    {
        var installDir = CreateTempInstallDir();
        try
        {
            CreateManifest(installDir);
            var handler = new FakeHttpMessageHandler((request, _) =>
            {
                Assert.Contains("appid=4129620", request.RequestUri!.Query);
                Assert.Contains("version=123", request.RequestUri.Query);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"response":{"success":true,"up_to_date":false}}"""),
                });
            });
            var sut = CreateService(new FakeSteamCmdService(), handler);

            var hasUpdate = await sut.IsUpdateAvailableAsync(installDir);

            Assert.True(hasUpdate);
        }
        finally
        {
            DeleteTempDir(installDir);
        }
    }

    private static ServerInstallService CreateService(ISteamCmdService steamCmd) =>
        CreateService(steamCmd, FakeHttpMessageHandler.ThrowsOffline());

    private static ServerInstallService CreateService(ISteamCmdService steamCmd, FakeHttpMessageHandler httpHandler) =>
        new(
            NullLogger<ServerInstallService>.Instance,
            steamCmd,
            NullAppSettingsService.Instance,
            new FakeHttpClientFactory(httpHandler));

    private static async Task<List<InstallProgress>> CollectAsync(IAsyncEnumerable<InstallProgress> source)
    {
        var result = new List<InstallProgress>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    private static string CreateTempInstallDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "wsm-install-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateManifest(string installDir)
    {
        var steamApps = Path.Combine(installDir, "steamapps");
        Directory.CreateDirectory(steamApps);
        var manifest = Path.Combine(steamApps, "appmanifest_4129620.acf");
        File.WriteAllText(manifest, "\"buildid\" \"123\"");
        return manifest;
    }

    private static void DeleteTempDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class FakeSteamCmdService : ISteamCmdService
    {
        private readonly IReadOnlyList<string> _lines;

        public FakeSteamCmdService(params string[] lines) => _lines = lines;

        public List<string> RunArguments { get; } = new();

        public async IAsyncEnumerable<string> EnsureSteamCmdAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<string> RunAsync(
            string arguments,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            RunArguments.Add(arguments);
            foreach (var line in _lines)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return line;
            }
        }
    }
}
