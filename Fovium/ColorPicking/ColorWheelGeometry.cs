namespace Fovium.ColorPicking;

internal readonly record struct ColorWheelPoint(double X, double Y);

internal static class ColorWheelGeometry
{
    public static HsvColor MapPointToHueSaturation(
        double width,
        double height,
        double x,
        double y,
        double value)
    {
        var centerX = Math.Max(0, width) / 2;
        var centerY = Math.Max(0, height) / 2;
        var radius = Math.Max(0, Math.Min(width, height) / 2);
        if (radius == 0)
        {
            return new HsvColor(0, 0, value).Normalize();
        }

        var dx = x - centerX;
        var dy = y - centerY;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var hue = ColorSelectionModel.WrapHue(Math.Atan2(dy, dx) * 180 / Math.PI);
        return new HsvColor(hue, Math.Clamp(distance / radius, 0, 1), value).Normalize();
    }

    public static ColorWheelPoint MapHueSaturationToPoint(
        double width,
        double height,
        double hueDegrees,
        double saturation)
    {
        var centerX = Math.Max(0, width) / 2;
        var centerY = Math.Max(0, height) / 2;
        var radius = Math.Max(0, Math.Min(width, height) / 2);
        var angle = ColorSelectionModel.WrapHue(hueDegrees) * Math.PI / 180;
        var distance = radius * Math.Clamp(double.IsFinite(saturation) ? saturation : 0, 0, 1);
        return new ColorWheelPoint(
            centerX + (Math.Cos(angle) * distance),
            centerY + (Math.Sin(angle) * distance));
    }

    public static double MapValueStripY(double height, double y)
    {
        if (!double.IsFinite(height) || height <= 0)
        {
            return 0;
        }

        return 1 - Math.Clamp(y / height, 0, 1);
    }

    public static double MapValueToStripY(double height, double value) =>
        Math.Max(0, height) * (1 - Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1));
}