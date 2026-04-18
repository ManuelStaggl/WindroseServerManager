using WindroseServerManager.Core.Models;
using Xunit;

namespace WindroseServerManager.Core.Tests;

public class WorldParameterCatalogTests
{
    [Fact]
    public void GetRange_MobHealth_ReturnsExpectedRange()
    {
        var (min, max, def) = WorldParameterCatalog.GetRange(WorldParameterCatalog.MobHealth);
        Assert.Equal(0.2, min);
        Assert.Equal(5.0, max);
        Assert.Equal(1.0, def);
    }

    [Fact]
    public void GetRange_ShipsHealth_HasMinimum04()
    {
        var (min, _, _) = WorldParameterCatalog.GetRange(WorldParameterCatalog.ShipsHealth);
        Assert.Equal(0.4, min);
    }

    [Fact]
    public void GetRange_ShipsDamage_HasMaximum25()
    {
        var (_, max, _) = WorldParameterCatalog.GetRange(WorldParameterCatalog.ShipsDamage);
        Assert.Equal(2.5, max);
    }

    [Fact]
    public void MakeKey_ProducesExpectedFormat()
    {
        var key = WorldParameterCatalog.MakeKey("WDS.Parameter.CombatDifficulty");
        Assert.Equal("{\"TagName\": \"WDS.Parameter.CombatDifficulty\"}", key);
    }
}
