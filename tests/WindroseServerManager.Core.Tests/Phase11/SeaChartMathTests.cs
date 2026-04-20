using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase11;

public class SeaChartMathTests
{
    private const double WorldMin = -30000;
    private const double WorldMax = 30000;
    private const double CanvasW = 800;
    private const double CanvasH = 600;

    private static (double cx, double cy) Map(double worldX, double worldY) =>
        SeaChartMath.WorldToCanvas(worldX, worldY, CanvasW, CanvasH, WorldMin, WorldMax, WorldMin, WorldMax);

    [Fact]
    public void WorldOrigin_MapsToCanvasCenter()
    {
        var (cx, cy) = Map(0, 0);

        Assert.Equal(400, cx, precision: 6);
        Assert.Equal(300, cy, precision: 6);
    }

    [Fact]
    public void TopLeftCorner_MapsToZeroZero()
    {
        // worldX=-30000, worldY=+30000 → canvas top-left (0,0) because Y is inverted
        var (cx, cy) = Map(-30000, 30000);

        Assert.Equal(0, cx, precision: 6);
        Assert.Equal(0, cy, precision: 6);
    }

    [Fact]
    public void BottomRightCorner_MapsToCanvasSize()
    {
        // worldX=+30000, worldY=-30000 → canvas bottom-right (800,600)
        var (cx, cy) = Map(30000, -30000);

        Assert.Equal(800, cx, precision: 6);
        Assert.Equal(600, cy, precision: 6);
    }

    [Fact]
    public void ZeroRangeX_ReturnsCenterFallback()
    {
        // worldMinX == worldMaxX → zero range
        var (cx, cy) = SeaChartMath.WorldToCanvas(0, 0, 800, 600, 100, 100, -30000, 30000);

        Assert.Equal(400, cx, precision: 6);
        Assert.Equal(300, cy, precision: 6);
    }

    [Fact]
    public void ZeroRangeY_ReturnsCenterFallback()
    {
        // worldMinY == worldMaxY → zero range
        var (cx, cy) = SeaChartMath.WorldToCanvas(0, 0, 800, 600, -30000, 30000, 100, 100);

        Assert.Equal(400, cx, precision: 6);
        Assert.Equal(300, cy, precision: 6);
    }

    [Fact]
    public void NegativeRange_ReturnsCenterFallback()
    {
        // worldMin > worldMax → negative range
        var (cx, cy) = SeaChartMath.WorldToCanvas(0, 0, 800, 600, 30000, -30000, 30000, -30000);

        Assert.Equal(400, cx, precision: 6);
        Assert.Equal(300, cy, precision: 6);
    }
}
