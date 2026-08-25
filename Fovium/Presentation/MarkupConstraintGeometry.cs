using Fovium.Rendering;

namespace Fovium.Presentation;

internal static class MarkupConstraintGeometry
{
    private const double AngleStep = Math.PI / 4;

    public static PointD SnapEndpointTo45Degrees(PointD start, PointD current)
    {
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(length) || length <= double.Epsilon)
        {
            return start;
        }

        var angle = Math.Atan2(dy, dx);
        var snappedAngle = Math.Round(angle / AngleStep, MidpointRounding.AwayFromZero) * AngleStep;
        return new PointD(
            start.X + Math.Cos(snappedAngle) * length,
            start.Y + Math.Sin(snappedAngle) * length);
    }

    public static PointD SquareEndpoint(PointD start, PointD current)
    {
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (!double.IsFinite(side))
        {
            return start;
        }

        var signX = dx < 0 ? -1 : 1;
        var signY = dy < 0 ? -1 : 1;
        return new PointD(start.X + signX * side, start.Y + signY * side);
    }

    public static PointD ClipEndpointAlongRay(PointD start, PointD endpoint, PixelSize sourceSize)
    {
        var dx = endpoint.X - start.X;
        var dy = endpoint.Y - start.Y;
        var factor = 1d;
        factor = LimitFactor(start.X, dx, 0, sourceSize.Width, factor);
        factor = LimitFactor(start.Y, dy, 0, sourceSize.Height, factor);
        return new PointD(start.X + dx * factor, start.Y + dy * factor);
    }

    private static double LimitFactor(
        double start,
        double delta,
        double minimum,
        double maximum,
        double currentFactor)
    {
        if (delta > 0 && start + delta > maximum)
        {
            return Math.Min(currentFactor, (maximum - start) / delta);
        }

        if (delta < 0 && start + delta < minimum)
        {
            return Math.Min(currentFactor, (minimum - start) / delta);
        }

        return currentFactor;
    }
}
