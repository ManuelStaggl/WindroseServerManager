using WindroseServerManager.Core.Models;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class ServerDescriptionTests
{
    [Fact]
    public void ValidateInviteCode_Null_ReturnsNull()
    {
        Assert.Null(ServerDescription.ValidateInviteCode(null));
    }

    [Fact]
    public void ValidateInviteCode_Empty_ReturnsNull()
    {
        Assert.Null(ServerDescription.ValidateInviteCode(string.Empty));
    }

    [Fact]
    public void ValidateInviteCode_TooShort_ReturnsError()
    {
        var result = ServerDescription.ValidateInviteCode("abc12");
        Assert.NotNull(result);
        Assert.Contains("6 Zeichen", result);
    }

    [Fact]
    public void ValidateInviteCode_InvalidChars_ReturnsError()
    {
        var result = ServerDescription.ValidateInviteCode("abc-123");
        Assert.NotNull(result);
        Assert.Contains("0-9", result);
    }

    [Fact]
    public void ValidateInviteCode_ValidSixChars_ReturnsNull()
    {
        Assert.Null(ServerDescription.ValidateInviteCode("Abc123"));
    }

    [Fact]
    public void ValidateInviteCode_ValidLong_ReturnsNull()
    {
        Assert.Null(ServerDescription.ValidateInviteCode("AhoyPirateXy"));
    }
}
