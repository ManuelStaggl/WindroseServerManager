using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase9;

public class RconPasswordGeneratorTests
{
    private const string UrlSafeAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [Fact]
    public void Generate_DefaultLength_Is24()
    {
        var pw = RconPasswordGenerator.Generate();
        Assert.Equal(24, pw.Length);
    }

    [Fact]
    public void Generate_CustomLength_IsRespected()
    {
        var pw = RconPasswordGenerator.Generate(32);
        Assert.Equal(32, pw.Length);
    }

    [Fact]
    public void Generate_OnlyContainsUrlSafeCharacters()
    {
        // 100 iterations to catch any stray character from randomness.
        for (int i = 0; i < 100; i++)
        {
            var pw = RconPasswordGenerator.Generate();
            Assert.All(pw, c => Assert.Contains(c, UrlSafeAlphabet));
        }
    }

    [Fact]
    public void Generate_TwoCalls_ReturnDifferentValues()
    {
        var a = RconPasswordGenerator.Generate();
        var b = RconPasswordGenerator.Generate();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Generate_LengthBelow16_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RconPasswordGenerator.Generate(15));
    }
}
