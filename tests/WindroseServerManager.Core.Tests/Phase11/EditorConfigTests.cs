using System.Linq;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase11;

public class EditorConfigTests
{
    [Fact]
    public void Schema_All_IsNonEmpty()
    {
        Assert.NotEmpty(WindrosePlusConfigSchema.All);
    }

    [Fact]
    public void Schema_All_ContainsXpKey()
    {
        Assert.Contains(WindrosePlusConfigSchema.All, s => s.Key == "xp");
    }

    [Fact]
    public void Schema_All_ContainsLootKey()
    {
        Assert.Contains(WindrosePlusConfigSchema.All, s => s.Key == "loot");
    }

    [Fact]
    public void Schema_All_ContainsHttpPortKey()
    {
        Assert.Contains(WindrosePlusConfigSchema.All, s => s.Key == "http_port");
    }

    [Fact]
    public void Schema_All_HasAtLeastThirteenEntries()
    {
        // 3 Server entries + 10 Multiplier entries = 13 minimum
        Assert.True(WindrosePlusConfigSchema.All.Count >= 13);
    }

    [Fact]
    public void Validate_Xp_ValidFloat_ReturnsNull()
    {
        Assert.Null(WindrosePlusConfigSchema.Validate("xp", "1.5"));
    }

    [Fact]
    public void Validate_Xp_NotANumber_ReturnsError()
    {
        Assert.NotNull(WindrosePlusConfigSchema.Validate("xp", "abc"));
    }

    [Fact]
    public void Validate_Xp_BelowMin_ReturnsError()
    {
        // Min is 0.1, so -1 is below
        Assert.NotNull(WindrosePlusConfigSchema.Validate("xp", "-1"));
    }

    [Fact]
    public void Validate_Xp_AboveMax_ReturnsError()
    {
        // Max is 100, so 101 is above
        Assert.NotNull(WindrosePlusConfigSchema.Validate("xp", "101"));
    }

    [Fact]
    public void Validate_HttpPort_ValidValue_ReturnsNull()
    {
        Assert.Null(WindrosePlusConfigSchema.Validate("http_port", "8780"));
    }

    [Fact]
    public void Validate_HttpPort_TooHigh_ReturnsError()
    {
        // Max is 65535, 70000 is above
        Assert.NotNull(WindrosePlusConfigSchema.Validate("http_port", "70000"));
    }

    [Fact]
    public void Validate_RconEnabled_ValidBool_ReturnsNull()
    {
        Assert.Null(WindrosePlusConfigSchema.Validate("rcon_enabled", "true"));
        Assert.Null(WindrosePlusConfigSchema.Validate("rcon_enabled", "false"));
    }

    [Fact]
    public void Validate_RconEnabled_NotBool_ReturnsError()
    {
        Assert.NotNull(WindrosePlusConfigSchema.Validate("rcon_enabled", "yes"));
    }

    [Fact]
    public void Validate_UnknownKey_ReturnsError()
    {
        Assert.NotNull(WindrosePlusConfigSchema.Validate("unknown_key_xyz", "value"));
    }
}
