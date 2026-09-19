using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorWheelGeometryTests
{
    [Fact]
    public void CenterMapsToZeroSaturationAndRightEdgeMapsToRed()
    {
        var center = ColorWheelGeometry.MapPointToHueSaturation(200, 200, 100, 100, 0.75);
        var edge = ColorWheelGeometry.MapPointToHueSaturation(200, 200, 200, 100, 0.75);

        Assert.Equal(0, center.Saturation);
        Assert.Equal(0.75, center.Value);
        Assert.Equal(0, edge.HueDegrees, 8);
        Assert.Equal(1, edge.Saturation, 8);
    }

    [Fact]
    public void DragOutsideCircleClampsSaturationWithoutChangingAngle()
    {
        var mapped = ColorWheelGeometry.MapPointToHueSaturation(200, 200, 400, 400, 1);

        Assert.Equal(45, mapped.HueDegrees, 8);
        Assert.Equal(1, mapped.Saturation, 8);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 0.25)]
    [InlineData(120, 0.5)]
    [InlineData(225, 0.8)]
    [InlineData(359, 1)]
    public void HueSaturationMarkerRoundTripsThroughWheelCoordinates(double hue, double saturation)
    {
        var point = ColorWheelGeometry.MapHueSaturationToPoint(320, 320, hue, saturation);
        var mapped = ColorWheelGeometry.MapPointToHueSaturation(320, 320, point.X, point.Y, 1);

        Assert.Equal(saturation, mapped.Saturation, 8);
        if (saturation > 0)
        {
            Assert.Equal(hue, mapped.HueDegrees, 8);
        }
    }

    [Fact]
    public void ValueStripMapsTopToOneBottomToZeroAndClampsOutside()
    {
        Assert.Equal(1, ColorWheelGeometry.MapValueStripY(300, -20));
        Assert.Equal(1, ColorWheelGeometry.MapValueStripY(300, 0));
        Assert.Equal(0.5, ColorWheelGeometry.MapValueStripY(300, 150));
        Assert.Equal(0, ColorWheelGeometry.MapValueStripY(300, 300));
        Assert.Equal(0, ColorWheelGeometry.MapValueStripY(300, 400));
        Assert.Equal(75, ColorWheelGeometry.MapValueToStripY(300, 0.75));
    }
}