using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase9;

public class SteamIdParserTests
{
    [Theory]
    [InlineData("76561198012345678", "76561198012345678")]
    [InlineData("https://steamcommunity.com/profiles/76561198012345678", "76561198012345678")]
    [InlineData("http://steamcommunity.com/profiles/76561198012345678/", "76561198012345678")]
    [InlineData("https://steamcommunity.com/profiles/76561198012345678/?xml=1", "76561198012345678")]
    [InlineData("   76561198012345678  ", "76561198012345678")]
    public void ExtractSteamId64_AcceptedInputs(string input, string expected)
    {
        Assert.Equal(expected, SteamIdParser.ExtractSteamId64(input));
    }

    [Theory]
    [InlineData("https://steamcommunity.com/id/somevanity")]
    [InlineData("https://steamcommunity.com/id/somevanity/")]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("12345678901234567")] // 17 digits but wrong prefix
    [InlineData("765611980123456789")] // 18 digits (too long)
    public void ExtractSteamId64_RejectedInputs(string? input)
    {
        Assert.Null(SteamIdParser.ExtractSteamId64(input));
    }
}
