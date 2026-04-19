using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class NexusUrlParserTests
{
    [Theory]
    [InlineData("https://www.nexusmods.com/windrose/mods/29", 29)]
    [InlineData("http://nexusmods.com/windrose/mods/29", 29)]
    [InlineData("www.nexusmods.com/windrose/mods/29", 29)]
    [InlineData("https://www.nexusmods.com/windrose/mods/29?tab=description", 29)]
    [InlineData("https://www.nexusmods.com/windrose/mods/29#files", 29)]
    [InlineData("HTTPS://WWW.NEXUSMODS.COM/WINDROSE/MODS/42", 42)]
    [InlineData("42", 42)]
    [InlineData("  42  ", 42)]
    public void TryParse_ValidInputs_ReturnsId(string input, int expected)
    {
        var ok = NexusUrlParser.TryParse(input, "windrose", out var id, out var reason);
        Assert.True(ok, reason);
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("random garbage")]
    [InlineData("https://example.com/mods/29")]
    public void TryParse_InvalidInputs_ReturnsFalse(string input)
    {
        var ok = NexusUrlParser.TryParse(input, "windrose", out _, out var reason);
        Assert.False(ok);
        Assert.NotNull(reason);
    }

    [Fact]
    public void TryParse_WrongDomain_ReturnsFalse()
    {
        var ok = NexusUrlParser.TryParse("https://www.nexusmods.com/skyrim/mods/29", "windrose", out _, out var reason);
        Assert.False(ok);
        Assert.Contains("skyrim", reason);
    }

    [Fact]
    public void TryParse_NegativeNumber_ReturnsFalse()
    {
        Assert.False(NexusUrlParser.TryParse("-5", "windrose", out _, out _));
    }

    [Fact]
    public void TryParse_Zero_ReturnsFalse()
    {
        Assert.False(NexusUrlParser.TryParse("0", "windrose", out _, out _));
    }

    [Theory]
    [InlineData("Expanded Horizons QOL Plus (Modular) v2.3-49-2-3-1776398317.zip", 49)]
    [InlineData("UnlimtedSwiming-55-1-1776350072.zip", 55)]
    [InlineData("ExtendedFeast-90-1-0-0-1776520235.zip", 90)]
    [InlineData("ExtendedFeast-90-1-0-0-1776520235.7z", 90)]
    [InlineData("SomeMod-123-2-5-1-1700000000.zip", 123)]
    public void TryExtractModIdFromArchiveName_NexusNaming_Works(string file, int expected)
    {
        Assert.Equal(expected, NexusUrlParser.TryExtractModIdFromArchiveName(file));
    }

    [Theory]
    [InlineData("MyMod.pak")]
    [InlineData("random.zip")]
    [InlineData("")]
    [InlineData("NoNumbers-hello-world.zip")]
    public void TryExtractModIdFromArchiveName_NonNexus_ReturnsNegative(string file)
    {
        Assert.Equal(-1, NexusUrlParser.TryExtractModIdFromArchiveName(file));
    }
}
