using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal enum PhotoGradientAxis
{
    Horizontal,
    Vertical,
}

internal readonly record struct PhotoLinearGradient(
    PhotoGradientAxis Axis,
    StageColor Start,
    StageColor Middle,
    StageColor End);

internal readonly record struct PhotoRadialGlow(
    StageColor Center,
    StageColor Middle,
    StageColor Edge);