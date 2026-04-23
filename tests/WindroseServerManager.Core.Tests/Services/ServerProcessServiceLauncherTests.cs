using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using WindroseServerManager.Core.Tests.Fixtures;
using WindroseServerManager.Core.Tests.TestDoubles;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

/// <summary>
/// Integration coverage for WPLUS-04: ServerProcessService must resolve its launcher through
/// <see cref="IWindrosePlusService.ResolveLauncher"/> rather than hardcoding the .exe path.
/// Because <see cref="ServerProcessService.StartAsync"/> spawns a real process, these tests
/// exercise the pure <c>ResolveLauncher</c> function directly. The ServerProcessService
/// constructor-level DI wiring is validated by the build.
/// </summary>
public class ServerProcessServiceLauncherTests
{
    private static WindrosePlusService CreateService(TempServerFixture fixture, TestLogger? logger = null)
    {
        var handler = FakeHttpMessageHandler.ThrowsOffline();
        var factory = new FakeHttpClientFactory(handler);
        ILogger<WindrosePlusService> log = logger is null
            ? NullLogger<WindrosePlusService>.Instance
            : new LoggerAdapter<WindrosePlusService>(logger);
        return new WindrosePlusService(log, factory, NullAppSettingsService.Instance, fixture.CacheDir);
    }

    private static ServerInstallInfo BuildInfo(string dir, bool active)
    {
        return new ServerInstallInfo(
            IsInstalled: Directory.Exists(dir),
            InstallDir: dir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: active,
            WindrosePlusVersionTag: null);
    }

    [Fact]
    public void Start_UsesExe_WhenActive_BecauseBatArchitectureWasRemoved()
    {
        // Historical context: earlier versions generated a StartWindrosePlusServer.bat
        // and launched that when WindrosePlus was active. The PS5-refactor replaced the
        // .bat launcher with a BuildPak pre-launch step + direct .exe start.
        // This test pins the new contract so nobody accidentally reintroduces the .bat.
        using var fixture = new TempServerFixture();
        File.WriteAllText(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat"), "irrelevant");

        var svc = CreateService(fixture);
        var info = BuildInfo(fixture.ServerDir, active: true);

        var (exe, _) = svc.ResolveLauncher(fixture.ServerDir, info);

        Assert.EndsWith("WindroseServer.exe", exe);
    }

    [Fact]
    public void Start_UsesExe_WhenOptedOut()
    {
        using var fixture = new TempServerFixture();
        // Even if a .bat exists, opt-out must pick the .exe.
        File.WriteAllText(Path.Combine(fixture.ServerDir, "StartWindrosePlusServer.bat"), "irrelevant");

        var svc = CreateService(fixture);
        var info = BuildInfo(fixture.ServerDir, active: false);

        var (exe, _) = svc.ResolveLauncher(fixture.ServerDir, info);

        Assert.EndsWith("WindroseServer.exe", exe);
    }

    // ---- Test doubles mirrored from WindrosePlusServiceTests (private there) ----

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
