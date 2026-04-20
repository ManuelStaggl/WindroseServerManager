namespace WindroseServerManager.Core.Services;

public static class SeaChartMath
{
    public static (double cx, double cy) WorldToCanvas(
        double worldX, double worldY,
        double canvasW, double canvasH,
        double worldMinX, double worldMaxX,
        double worldMinY, double worldMaxY)
    {
        var rangeX = worldMaxX - worldMinX;
        var rangeY = worldMaxY - worldMinY;
        if (rangeX <= 0 || rangeY <= 0) return (canvasW / 2, canvasH / 2);
        var cx = (worldX - worldMinX) / rangeX * canvasW;
        var cy = (worldMaxY - worldY) / rangeY * canvasH;
        return (cx, cy);
    }
}
