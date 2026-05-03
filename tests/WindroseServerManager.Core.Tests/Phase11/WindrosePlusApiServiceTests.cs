using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using WindroseServerManager.Core.Tests.TestDoubles;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase11;

public class WindrosePlusApiServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WindrosePlusApiServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wrsm-apiservice-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static WindrosePlusApiService CreateService(
        HttpMessageHandler? handler = null,
        string? serverDir = null,
        int port = 0,
        string password = "")
    {
        var settings = new FakeApiSettings();
        if (serverDir is not null && port > 0)
        {
            settings.Current.WindrosePlusDashboardPortByServer[serverDir] = port;
            settings.Current.WindrosePlusRconPasswordByServer[serverDir] = password;
        }

        var httpFactory = new FakeHttpClientFactory(handler ?? new ThrowIfCalledHandler());
        return new WindrosePlusApiService(httpFactory, settings, NullLogger<WindrosePlusApiService>.Instance);
    }

    // --- Command builder tests ---

    [Fact]
    public void BuildKickCommand_ReturnsExpectedFormat()
    {
        var svc = CreateService();
        Assert.Equal("wp.kick 76561198012345678", svc.BuildKickCommand("76561198012345678"));
    }

    [Fact]
    public void BuildBanCommand_Permanent_NoMinutes()
    {
        var svc = CreateService();
        Assert.Equal("wp.ban 76561198012345678", svc.BuildBanCommand("76561198012345678", null));
    }

    [Fact]
    public void BuildBanCommand_Timed_IncludesMinutes()
    {
        var svc = CreateService();
        Assert.Equal("wp.ban 76561198012345678 60", svc.BuildBanCommand("76561198012345678", 60));
    }

    [Fact]
    public void BuildBroadcastCommand_PrefixesWpSay()
    {
        var svc = CreateService();
        Assert.Equal("wp.say Server restart in 5 min", svc.BuildBroadcastCommand("Server restart in 5 min"));
    }

    // --- RCON injection prevention tests ---

    [Fact]
    public void SanitizeRconParameter_StripsNewlines()
    {
        var result = WindrosePlusApiService.SanitizeRconParameter("hello\nwp.kick AllPlayers");
        Assert.Equal("hellowp.kick AllPlayers", result);
    }

    [Fact]
    public void SanitizeRconParameter_StripsCarriageReturns()
    {
        var result = WindrosePlusApiService.SanitizeRconParameter("hello\r\nwp.ban everyone");
        Assert.Equal("hellowp.ban everyone", result);
    }

    [Fact]
    public void SanitizeRconParameter_StripsNullBytes()
    {
        var result = WindrosePlusApiService.SanitizeRconParameter("test\0injection");
        Assert.Equal("testinjection", result);
    }

    [Fact]
    public void SanitizeRconParameter_PreservesNormalText()
    {
        var result = WindrosePlusApiService.SanitizeRconParameter("Server restart in 5 min!");
        Assert.Equal("Server restart in 5 min!", result);
    }

    [Fact]
    public void BuildBroadcastCommand_SanitizesInjection()
    {
        var svc = CreateService();
        var result = svc.BuildBroadcastCommand("hello\nwp.kick AllPlayers");
        Assert.DoesNotContain('\n', result);
        Assert.StartsWith("wp.say ", result);
    }

    [Fact]
    public void BuildKickCommand_SanitizesInjection()
    {
        var svc = CreateService();
        var result = svc.BuildKickCommand("player\nwp.ban all");
        Assert.DoesNotContain('\n', result);
    }

    [Fact]
    public void SanitizeRconParameter_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", WindrosePlusApiService.SanitizeRconParameter(""));
    }

    [Fact]
    public void SanitizeRconParameter_NullString_ReturnsNull()
    {
        Assert.Null(WindrosePlusApiService.SanitizeRconParameter(null!));
    }

    // --- Port guard tests ---

    [Fact]
    public async Task RconAsync_PortZero_ReturnsNull()
    {
        var svc = CreateService(); // no port registered → port 0
        var result = await svc.RconAsync(_tempDir, "test", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStatusAsync_PortZero_ReturnsNull()
    {
        var svc = CreateService();
        var result = await svc.GetStatusAsync(_tempDir, CancellationToken.None);
        Assert.Null(result);
    }

    // --- ReadConfig tests ---

    [Fact]
    public void ReadConfig_FileMissing_ReturnsNull()
    {
        var svc = CreateService();
        var result = svc.ReadConfig(_tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void ReadConfig_ValidJson_ReturnsPopulatedConfig()
    {
        var configPath = Path.Combine(_tempDir, "windrose_plus.json");
        var json = """
            {
                "Server": { "http_port": 8780 },
                "Multipliers": { "xp": 1.5 }
            }
            """;
        File.WriteAllText(configPath, json);

        var svc = CreateService();
        var config = svc.ReadConfig(_tempDir);

        Assert.NotNull(config);
        Assert.True(config!.Server.ContainsKey("http_port"));
        Assert.True(config.Multipliers.ContainsKey("xp"));
    }

    // --- WriteConfigAsync test ---

    [Fact]
    public async Task WriteConfigAsync_WritesThenMovesAtomically()
    {
        var svc = CreateService();
        var config = new WindrosePlusConfig();
        config.Server["http_port"] = 8780;
        config.Multipliers["xp"] = 1.5;

        await svc.WriteConfigAsync(_tempDir, config, CancellationToken.None);

        var configPath = Path.Combine(_tempDir, "windrose_plus.json");
        var tmpPath = configPath + ".tmp";

        Assert.True(File.Exists(configPath), "Target config file should exist after write");
        Assert.False(File.Exists(tmpPath), ".tmp file should be cleaned up after atomic move");

        var written = await File.ReadAllTextAsync(configPath);
        Assert.Contains("http_port", written);
    }

    // --- Nested helpers ---

    private sealed class FakeApiSettings : IAppSettingsService
    {
        public AppSettings Current { get; } = new();
        public event Action<AppSettings>? Changed;
        public string ActiveServerDir => Current.ServerInstallDir;
        public Task SelectServerAsync(string id) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
        {
            mutate(Current);
            Changed?.Invoke(Current);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowIfCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP call should not have been made.");
    }
}
