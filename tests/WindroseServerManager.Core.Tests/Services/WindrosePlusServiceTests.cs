using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using WindroseServerManager.Core.Tests.Fixtures;
using WindroseServerManager.Core.Tests.TestDoubles;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

/// <summary>
/// Behavior contract for <see cref="IWindrosePlusService"/>. Plan 02 unskipped these tests and wired them against
/// the concrete <see cref="WindrosePlusService"/>.
/// </summary>
public class WindrosePlusServiceTests
{
    private static WindrosePlusService CreateService(
        TempServerFixture fixture,
        FakeHttpMessageHandler handler,
        TestLogger? logger = null)
    {
        var factory = new FakeHttpClientFactory(handler);
        ILogger<WindrosePlusService> log = logger is null
            ? NullLogger<WindrosePlusService>.Instance
            : new LoggerAdapter<WindrosePlusService>(logger);
        return new WindrosePlusService(log, factory, NullAppSettingsService.Instance, fixture.CacheDir);
    }

    // ---------- WPLUS-01: Fetch + offline/cache ----------

    [Fact]
    public async Task FetchLatest_ParsesTagAndDigest()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        var release = await svc.FetchLatestAsync(CancellationToken.None);
        Assert.Equal("v1.0.6", release.Tag);
        Assert.NotNull(release.DigestSha256);
        Assert.StartsWith("sha256:", release.DigestSha256);
    }

    [Fact]
    public async Task FetchLatest_AcceptsMissingDigest_WithWarning()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer { PublishDigest = false };
        var logger = new TestLogger();
        var svc = CreateService(fixture, github.CreateHandler(), logger);
        var release = await svc.FetchLatestAsync(CancellationToken.None);
        Assert.Null(release.DigestSha256);
        Assert.Contains(logger.Warnings, w => w.Contains("digest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Install_UsesCache_WhenApiUnreachable_AndCacheExists()
    {
        using var fixture = new TempServerFixture();
        // Seed cache dir with a prior valid archive.
        var (bytes, _) = SampleArchiveBuilder.BuildWindrosePlusZip();
        File.WriteAllBytes(Path.Combine(fixture.CacheDir, "WindrosePlus.zip"), bytes);
        // Use a live github handler so the happy cache-exists path runs end-to-end.
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        var result = await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        Assert.Equal("v1.0.6", result.Tag);
    }

    [Fact]
    public async Task Install_ThrowsOfflineInstallException_WhenNoCache()
    {
        using var fixture = new TempServerFixture();
        var svc = CreateService(fixture, FakeHttpMessageHandler.ThrowsOffline());
        await Assert.ThrowsAsync<WindrosePlusOfflineException>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
    }

    // ---------- WPLUS-02: Digest + atomicity + preservation ----------

    [Fact]
    public async Task Install_ThrowsShaMismatch_WhenArchiveModified()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer { TamperArchive = true };
        var svc = CreateService(fixture, github.CreateHandler());
        await Assert.ThrowsAsync<WindrosePlusDigestMismatchException>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir()
    {
        using var fixture = new TempServerFixture();
        var exePath = Path.Combine(fixture.ServerDir, "WindroseServer.exe");
        var before = File.ReadAllText(exePath);
        var github = new FakeGithubReleaseServer { FailWindrosePlusAsset = true };
        var svc = CreateService(fixture, github.CreateHandler());
        await Assert.ThrowsAnyAsync<Exception>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(exePath));
        Assert.False(File.Exists(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat")));
    }

    [Fact]
    public async Task Install_PreservesExistingUserConfig()
    {
        using var fixture = new TempServerFixture();
        fixture.SeedExistingUserConfig("windrose_plus.json", "USER_DATA");
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        var contents = File.ReadAllText(Path.Combine(fixture.ServerDir, "windrose_plus.json"));
        Assert.Contains("USER_DATA", contents);
    }

    [Fact]
    public async Task Install_OverwritesVendorBinaries()
    {
        using var fixture = new TempServerFixture();
        var batPath = Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat");
        File.WriteAllText(batPath, "OLD");
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        Assert.NotEqual("OLD", File.ReadAllText(batPath));
    }

    [Fact]
    public async Task Install_WritesVersionMarker()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        var markerPath = Path.Combine(fixture.ServerDir, ".wplus-version");
        Assert.True(File.Exists(markerPath));
        var marker = JsonSerializer.Deserialize<WindrosePlusVersionMarker>(File.ReadAllText(markerPath));
        Assert.NotNull(marker);
        Assert.Equal("v1.0.6", marker!.Tag);
    }

    // ---------- WPLUS-03: License handling (MIT compliance) ----------

    [Fact]
    public async Task Install_PreservesLicenseUnderWindrosePlusFolder()
    {
        // MIT-Compliance per the upstream maintainer's request: the LICENSE file must
        // stay next to the mod ("as-is"). install.ps1 doesn't place it under
        // windrose_plus/ itself, so we copy it there before the root cleanup runs.
        // This pins both halves of the contract: LICENSE preserved under the mod,
        // staged files (LICENSE/README.md) removed from the server root.
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer();
        var svc = CreateService(fixture, github.CreateHandler());
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);

        var preservedLicense = Path.Combine(fixture.ServerDir, "windrose_plus", "LICENSE");
        Assert.True(File.Exists(preservedLicense), "Upstream LICENSE must be preserved under windrose_plus/.");
        Assert.Contains("MIT License", File.ReadAllText(preservedLicense));

        // Root cleanup still happens — staged files are gone after install.
        Assert.False(File.Exists(Path.Combine(fixture.ServerDir, "LICENSE")));
        Assert.False(File.Exists(Path.Combine(fixture.ServerDir, "README.md")));
    }

    // ---------- WPLUS-04: Launcher resolution ----------

    [Fact]
    public void ResolveLauncher_OptedOut_ReturnsExe()
    {
        using var fixture = new TempServerFixture();
        var svc = CreateService(fixture, FakeHttpMessageHandler.ThrowsOffline());
        var info = new ServerInstallInfo(
            IsInstalled: true,
            InstallDir: fixture.ServerDir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: false,
            WindrosePlusVersionTag: null);
        var (exe, args) = svc.ResolveLauncher(fixture.ServerDir, info);
        Assert.EndsWith("WindroseServer.exe", exe, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, args);
    }

    [Fact]
    public void ResolveLauncher_Active_ReturnsExe_BecauseBatArchitectureWasRemoved()
    {
        // Historical context: the launcher used to wrap WindrosePlus startup in a
        // generated StartWindrosePlusServer.bat. The PS5-refactor moved that work into
        // a BuildPak pre-launch PowerShell step + a direct .exe start. This test pins
        // the new contract — even with a leftover .bat present, the launcher returns the .exe.
        using var fixture = new TempServerFixture();
        File.WriteAllText(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat"), "irrelevant");
        var svc = CreateService(fixture, FakeHttpMessageHandler.ThrowsOffline());
        var info = new ServerInstallInfo(
            IsInstalled: true,
            InstallDir: fixture.ServerDir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: true,
            WindrosePlusVersionTag: "v1.0.6");
        var (exe, args) = svc.ResolveLauncher(fixture.ServerDir, info);
        Assert.EndsWith("WindroseServer.exe", exe, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, args);
    }

    // Inline test double — records Warning/Error messages.
    private sealed class TestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            if (logLevel == LogLevel.Warning) Warnings.Add(msg);
            else if (logLevel == LogLevel.Error) Errors.Add(msg);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>Adapts a non-generic <see cref="ILogger"/> to <see cref="ILogger{T}"/>.</summary>
    private sealed class LoggerAdapter<T> : ILogger<T>
    {
        private readonly ILogger _inner;
        public LoggerAdapter(ILogger inner) => _inner = inner;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
