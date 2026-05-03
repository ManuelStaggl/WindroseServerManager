using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ServerEventLog"/>.
/// Uses reflection to redirect the internal _filePath to a temp directory
/// so tests are fully isolated from the real AppData location.
/// </summary>
public sealed class ServerEventLogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFilePath;
    private readonly ServerEventLog _sut;

    public ServerEventLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wrsm-eventlog-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempFilePath = Path.Combine(_tempDir, "events.jsonl");

        _sut = new ServerEventLog(NullLogger<ServerEventLog>.Instance);
        RedirectFilePath(_sut, _tempFilePath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // --------------------------------------------------------------------------
    // 1. AppendAsync writes a valid JSON line to the file
    // --------------------------------------------------------------------------

    [Fact]
    public async Task AppendAsync_WritesJsonLine_ToFile()
    {
        var evt = CreateEvent(ServerEventType.Started, "manual start");

        await _sut.AppendAsync(evt);

        Assert.True(File.Exists(_tempFilePath));
        var lines = await File.ReadAllLinesAsync(_tempFilePath);
        Assert.Single(lines);
        // ServerEventLog uses camelCase naming; enums serialize as integers by default.
        Assert.Contains("\"type\":0", lines[0]);
        Assert.Contains("\"reason\":\"manual start\"", lines[0]);
    }

    // --------------------------------------------------------------------------
    // 2. AppendAsync fires the Appended event with the correct event
    // --------------------------------------------------------------------------

    [Fact]
    public async Task AppendAsync_FiresAppendedEvent()
    {
        var evt = CreateEvent(ServerEventType.Crashed, "segfault");
        ServerEvent? received = null;
        _sut.Appended += e => received = e;

        await _sut.AppendAsync(evt);

        Assert.NotNull(received);
        Assert.Equal(ServerEventType.Crashed, received!.Type);
        Assert.Equal("segfault", received.Reason);
        Assert.Equal(evt.TimestampUtc, received.TimestampUtc);
    }

    // --------------------------------------------------------------------------
    // 3. ReadRecentAsync returns empty when no file exists
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ReadRecentAsync_ReturnsEmpty_WhenNoFile()
    {
        // File does not exist yet — never appended anything.
        var result = await _sut.ReadRecentAsync();

        Assert.Empty(result);
    }

    // --------------------------------------------------------------------------
    // 4. ReadRecentAsync returns events newest-first
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ReadRecentAsync_ReturnsNewestFirst()
    {
        var first = CreateEvent(ServerEventType.Started, "first", DateTime.UtcNow.AddMinutes(-2));
        var second = CreateEvent(ServerEventType.Stopped, "second", DateTime.UtcNow.AddMinutes(-1));
        var third = CreateEvent(ServerEventType.Crashed, "third", DateTime.UtcNow);

        await _sut.AppendAsync(first);
        await _sut.AppendAsync(second);
        await _sut.AppendAsync(third);

        var result = await _sut.ReadRecentAsync();

        Assert.Equal(3, result.Count);
        // Newest first.
        Assert.Equal("third", result[0].Reason);
        Assert.Equal("second", result[1].Reason);
        Assert.Equal("first", result[2].Reason);
    }

    // --------------------------------------------------------------------------
    // 5. ReadRecentAsync respects the maxCount parameter
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ReadRecentAsync_RespectsMaxCount()
    {
        for (int i = 0; i < 5; i++)
            await _sut.AppendAsync(CreateEvent(ServerEventType.BackupManual, $"backup-{i}"));

        var result = await _sut.ReadRecentAsync(maxCount: 2);

        Assert.Equal(2, result.Count);
        // Should be the two newest.
        Assert.Equal("backup-4", result[0].Reason);
        Assert.Equal("backup-3", result[1].Reason);
    }

    // --------------------------------------------------------------------------
    // 6. ReadRecentAsync skips corrupt (unparseable) lines
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ReadRecentAsync_SkipsCorruptLines()
    {
        var valid = CreateEvent(ServerEventType.Started, "good event");
        await _sut.AppendAsync(valid);

        // Inject a corrupt line directly into the file.
        await File.AppendAllTextAsync(_tempFilePath, "NOT VALID JSON {{{" + Environment.NewLine);

        var another = CreateEvent(ServerEventType.Stopped, "also good");
        await _sut.AppendAsync(another);

        var result = await _sut.ReadRecentAsync();

        // The corrupt line in the middle should be silently skipped.
        Assert.Equal(2, result.Count);
        Assert.Equal("also good", result[0].Reason);
        Assert.Equal("good event", result[1].Reason);
    }

    // --------------------------------------------------------------------------
    // 7. ReadRecentAsync skips blank lines
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ReadRecentAsync_SkipsBlankLines()
    {
        var valid = CreateEvent(ServerEventType.AutoRestartHighRam, "memory pressure");
        await _sut.AppendAsync(valid);

        // Inject blank lines directly.
        await File.AppendAllTextAsync(_tempFilePath, Environment.NewLine);
        await File.AppendAllTextAsync(_tempFilePath, "   " + Environment.NewLine);
        await File.AppendAllTextAsync(_tempFilePath, "\t" + Environment.NewLine);

        var another = CreateEvent(ServerEventType.ScheduledRestart, "timer fired");
        await _sut.AppendAsync(another);

        var result = await _sut.ReadRecentAsync();

        // Only the two real events should appear — blank lines ignored.
        Assert.Equal(2, result.Count);
        Assert.Equal("timer fired", result[0].Reason);
        Assert.Equal("memory pressure", result[1].Reason);
    }

    // --------------------------------------------------------------------------
    // 8. ClearAsync empties the file
    // --------------------------------------------------------------------------

    [Fact]
    public async Task ClearAsync_EmptiesFile()
    {
        await _sut.AppendAsync(CreateEvent(ServerEventType.BackupAutomatic, "auto backup"));
        await _sut.AppendAsync(CreateEvent(ServerEventType.BackupRestored, "restored"));
        Assert.True(File.Exists(_tempFilePath));
        Assert.NotEmpty(await File.ReadAllLinesAsync(_tempFilePath));

        await _sut.ClearAsync();

        Assert.True(File.Exists(_tempFilePath));
        var contents = await File.ReadAllTextAsync(_tempFilePath);
        Assert.Equal(string.Empty, contents);

        // Reading after clear should yield empty.
        var result = await _sut.ReadRecentAsync();
        Assert.Empty(result);
    }

    // --------------------------------------------------------------------------
    // 9. AppendAsync under concurrent writes loses no data
    // --------------------------------------------------------------------------

    [Fact]
    public async Task AppendAsync_MultipleWrites_NoDataLoss()
    {
        const int totalWrites = 50;
        var tasks = Enumerable.Range(0, totalWrites)
            .Select(i => _sut.AppendAsync(CreateEvent(ServerEventType.Started, $"concurrent-{i}")));

        await Task.WhenAll(tasks);

        var result = await _sut.ReadRecentAsync(maxCount: totalWrites + 10);
        Assert.Equal(totalWrites, result.Count);

        // Every event reason must be present exactly once.
        var reasons = result.Select(e => e.Reason).ToHashSet();
        for (int i = 0; i < totalWrites; i++)
            Assert.Contains($"concurrent-{i}", reasons);
    }

    // ==========================================================================
    // Helpers
    // ==========================================================================

    private static ServerEvent CreateEvent(
        ServerEventType type,
        string reason,
        DateTime? timestampUtc = null)
    {
        return new ServerEvent(
            timestampUtc ?? DateTime.UtcNow,
            type,
            reason);
    }

    /// <summary>
    /// Uses reflection to reassign the private <c>_filePath</c> field so the
    /// <see cref="ServerEventLog"/> reads/writes to our temp directory instead
    /// of the real AppData location.
    /// </summary>
    private static void RedirectFilePath(ServerEventLog instance, string path)
    {
        var field = typeof(ServerEventLog).GetField(
            "_filePath",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(instance, path);
    }
}
