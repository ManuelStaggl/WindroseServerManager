using System;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase11;

public class EventsLogParserTests
{
    // --- TryParseLine ---

    [Fact]
    public void TryParseLine_ValidJoin_ReturnsPopulatedEvent()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";

        var evt = EventsLogParser.TryParseLine(line);

        Assert.NotNull(evt);
        Assert.Equal("join", evt!.Type);
        Assert.Equal("76561198012345678", evt.SteamId);
        Assert.Equal("Bob", evt.Name);
        Assert.Equal(new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc), evt.Timestamp);
    }

    [Fact]
    public void TryParseLine_ValidLeave_ReturnsEvent()
    {
        const string line = """{"type":"leave","steamId":"76561198000000001","name":"Alice","timestamp":"2026-04-20T13:00:00Z"}""";

        var evt = EventsLogParser.TryParseLine(line);

        Assert.NotNull(evt);
        Assert.Equal("leave", evt!.Type);
        Assert.Equal("Alice", evt.Name);
    }

    [Fact]
    public void TryParseLine_EmptyString_ReturnsNull()
    {
        Assert.Null(EventsLogParser.TryParseLine(string.Empty));
    }

    [Fact]
    public void TryParseLine_Whitespace_ReturnsNull()
    {
        Assert.Null(EventsLogParser.TryParseLine("   "));
    }

    [Fact]
    public void TryParseLine_MalformedJson_ReturnsNull()
    {
        Assert.Null(EventsLogParser.TryParseLine("{not valid json"));
    }

    [Fact]
    public void TryParseLine_UnknownType_ReturnsNull()
    {
        const string line = """{"type":"chat","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";

        Assert.Null(EventsLogParser.TryParseLine(line));
    }

    [Fact]
    public void TryParseLine_MissingName_UsesUnknown()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","timestamp":"2026-04-20T12:00:00Z"}""";

        var evt = EventsLogParser.TryParseLine(line);

        Assert.NotNull(evt);
        Assert.Equal("Unknown", evt!.Name);
    }

    [Fact]
    public void TryParseLine_MissingTimestamp_UsesUtcNow()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob"}""";
        var before = DateTime.UtcNow;

        var evt = EventsLogParser.TryParseLine(line);

        var after = DateTime.UtcNow;
        Assert.NotNull(evt);
        Assert.True(evt!.Timestamp >= before.AddSeconds(-1));
        Assert.True(evt.Timestamp <= after.AddSeconds(1));
    }

    [Fact]
    public void TryParseLine_InvalidTimestamp_UsesUtcNow()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"not-a-date"}""";
        var before = DateTime.UtcNow;

        var evt = EventsLogParser.TryParseLine(line);

        var after = DateTime.UtcNow;
        Assert.NotNull(evt);
        Assert.True(evt!.Timestamp >= before.AddSeconds(-1));
        Assert.True(evt.Timestamp <= after.AddSeconds(1));
    }

    // --- MatchesFilter ---

    [Fact]
    public void MatchesFilter_EmptyFilter_ReturnsTrue()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";
        var evt = EventsLogParser.TryParseLine(line)!;

        Assert.True(EventsLogParser.MatchesFilter(evt, string.Empty));
    }

    [Fact]
    public void MatchesFilter_MatchesNameCaseInsensitive()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";
        var evt = EventsLogParser.TryParseLine(line)!;

        Assert.True(EventsLogParser.MatchesFilter(evt, "bob"));
        Assert.True(EventsLogParser.MatchesFilter(evt, "BOB"));
    }

    [Fact]
    public void MatchesFilter_MatchesSteamId()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";
        var evt = EventsLogParser.TryParseLine(line)!;

        Assert.True(EventsLogParser.MatchesFilter(evt, "76561"));
    }

    [Fact]
    public void MatchesFilter_MatchesType()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";
        var evt = EventsLogParser.TryParseLine(line)!;

        Assert.True(EventsLogParser.MatchesFilter(evt, "join"));
    }

    [Fact]
    public void MatchesFilter_NoMatch_ReturnsFalse()
    {
        const string line = """{"type":"join","steamId":"76561198012345678","name":"Bob","timestamp":"2026-04-20T12:00:00Z"}""";
        var evt = EventsLogParser.TryParseLine(line)!;

        Assert.False(EventsLogParser.MatchesFilter(evt, "xyz_no_match_xyz"));
    }
}
