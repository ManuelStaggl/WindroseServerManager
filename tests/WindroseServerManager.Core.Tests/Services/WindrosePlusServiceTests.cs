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
/// Behavior contract for <see cref="IWindrosePlusService"/>. All tests are <c>[Fact(Skip = ...)]</c> in Wave 0
/// because the concrete <c>WindrosePlusService</c> implementation does not yet exist — it is created in Plan 02,
/// which also removes the Skip argument on every test here. The test file compiles today against the interface only
/// (<c>IWindrosePlusService svc = null!;</c>); Plan 02 replaces the <c>null!</c> with a real construction.
/// </summary>
public class WindrosePlusServiceTests
{
    private const string SkipReason = "Enabled by Plan 02 — WindrosePlusService not yet implemented";

    // ---------- WPLUS-01: Fetch + offline/cache ----------

    [Fact(Skip = SkipReason)]
    public async Task FetchLatest_ParsesTagAndDigest()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer();
        IWindrosePlusService svc = null!;
        var release = await svc.FetchLatestAsync(CancellationToken.None);
        Assert.Equal("v1.0.6", release.Tag);
        Assert.NotNull(release.DigestSha256);
        Assert.StartsWith("sha256:", release.DigestSha256);
    }

    [Fact(Skip = SkipReason)]
    public async Task FetchLatest_AcceptsMissingDigest_WithWarning()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer { PublishDigest = false };
        IWindrosePlusService svc = null!;
        var release = await svc.FetchLatestAsync(CancellationToken.None);
        Assert.Null(release.DigestSha256);
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_UsesCache_WhenApiUnreachable_AndCacheExists()
    {
        using var fixture = new TempServerFixture();
        // Seed cache dir with a prior valid archive + metadata (shape owned by Plan 02).
        var (bytes, _) = SampleArchiveBuilder.BuildWindrosePlusZip();
        File.WriteAllBytes(Path.Combine(fixture.CacheDir, "WindrosePlus.zip"), bytes);
        IWindrosePlusService svc = null!;
        var result = await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        Assert.Equal("v1.0.6", result.Tag);
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_ThrowsOfflineInstallException_WhenNoCache()
    {
        using var fixture = new TempServerFixture();
        IWindrosePlusService svc = null!;
        await Assert.ThrowsAsync<WindrosePlusOfflineException>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
    }

    // ---------- WPLUS-02: Digest + atomicity + preservation ----------

    [Fact(Skip = SkipReason)]
    public async Task Install_ThrowsShaMismatch_WhenArchiveModified()
    {
        using var fixture = new TempServerFixture();
        var github = new FakeGithubReleaseServer { TamperArchive = true };
        IWindrosePlusService svc = null!;
        await Assert.ThrowsAsync<WindrosePlusDigestMismatchException>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir()
    {
        using var fixture = new TempServerFixture();
        var exePath = Path.Combine(fixture.ServerDir, "WindroseServer.exe");
        var before = File.ReadAllText(exePath);
        var github = new FakeGithubReleaseServer { FailWindrosePlusAsset = true };
        IWindrosePlusService svc = null!;
        await Assert.ThrowsAnyAsync<Exception>(
            () => svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(exePath));
        Assert.False(File.Exists(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat")));
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_PreservesExistingUserConfig()
    {
        using var fixture = new TempServerFixture();
        fixture.SeedExistingUserConfig("windrose_plus.json", "USER_DATA");
        IWindrosePlusService svc = null!;
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        var contents = File.ReadAllText(Path.Combine(fixture.ServerDir, "windrose_plus.json"));
        Assert.Contains("USER_DATA", contents);
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_OverwritesVendorBinaries()
    {
        using var fixture = new TempServerFixture();
        var batPath = Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat");
        File.WriteAllText(batPath, "OLD");
        IWindrosePlusService svc = null!;
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        Assert.NotEqual("OLD", File.ReadAllText(batPath));
    }

    [Fact(Skip = SkipReason)]
    public async Task Install_WritesVersionMarker()
    {
        using var fixture = new TempServerFixture();
        IWindrosePlusService svc = null!;
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        var markerPath = Path.Combine(fixture.ServerDir, ".wplus-version");
        Assert.True(File.Exists(markerPath));
        var marker = JsonSerializer.Deserialize<WindrosePlusVersionMarker>(File.ReadAllText(markerPath));
        Assert.NotNull(marker);
        Assert.Equal("v1.0.6", marker!.Tag);
    }

    // ---------- WPLUS-03: License bundling ----------

    [Fact(Skip = SkipReason)]
    public async Task Install_CopiesLicenseToServerDir()
    {
        using var fixture = new TempServerFixture();
        IWindrosePlusService svc = null!;
        await svc.InstallAsync(fixture.ServerDir, progress: null, CancellationToken.None);
        var licensePath = Path.Combine(fixture.ServerDir, "WindrosePlus-LICENSE.txt");
        Assert.True(File.Exists(licensePath));
        Assert.Contains("MIT License", File.ReadAllText(licensePath));
    }

    // ---------- WPLUS-04: Launcher resolution ----------

    [Fact(Skip = SkipReason)]
    public void ResolveLauncher_OptedOut_ReturnsExe()
    {
        using var fixture = new TempServerFixture();
        IWindrosePlusService svc = null!;
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

    [Fact(Skip = SkipReason)]
    public void ResolveLauncher_Active_ReturnsBat()
    {
        using var fixture = new TempServerFixture();
        File.WriteAllText(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat"), "@echo off");
        IWindrosePlusService svc = null!;
        var info = new ServerInstallInfo(
            IsInstalled: true,
            InstallDir: fixture.ServerDir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: true,
            WindrosePlusVersionTag: "v1.0.6");
        var (exe, args) = svc.ResolveLauncher(fixture.ServerDir, info);
        Assert.EndsWith("StartWindrosePlusServer.bat", exe, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, args);
    }

    [Fact(Skip = SkipReason)]
    public void ResolveLauncher_Active_BatMissing_FallsBackWithWarning()
    {
        using var fixture = new TempServerFixture();
        // .bat intentionally NOT created — fallback path.
        var logger = new TestLogger();
        IWindrosePlusService svc = null!; // Plan 02 replaces with: new WindrosePlusService(logger, ...)
        var info = new ServerInstallInfo(
            IsInstalled: true,
            InstallDir: fixture.ServerDir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: true,
            WindrosePlusVersionTag: "v1.0.6");
        var (exe, _) = svc.ResolveLauncher(fixture.ServerDir, info);
        Assert.EndsWith("WindroseServer.exe", exe, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(logger.Warnings, w => w.Contains("StartWindrosePlusServer.bat"));
    }

    // Inline test double — records Warning/Error messages. Usable by Plan 02 as ILogger<WindrosePlusService>
    // once the concrete class exists (generic variance / interface accepts non-generic ILogger too).
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
}
