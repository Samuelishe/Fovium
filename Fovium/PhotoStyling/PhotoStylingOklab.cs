using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal readonly record struct PhotoStylingOklab(double L, double A, double B)
{
    public double Chroma => Math.Sqrt((A * A) + (B * B));

    public double Hue => Math.Atan2(B, A);

    public static PhotoStylingOklab Lerp(
        PhotoStylingOklab first,
        PhotoStylingOklab second,
        double amount) => new(
            first.L + ((second.L - first.L) * amount),
            first.A + ((second.A - first.A) * amount),
            first.B + ((second.B - first.B) * amount));

    public static PhotoStylingOklab FromSrgb(StageColor color)
    {
        var red = ToLinear(color.Red / 255d);
        var green = ToLinear(color.Green / 255d);
        var blue = ToLinear(color.Blue / 255d);
        var l = (0.4122214708 * red) + (0.5363325363 * green) + (0.0514459929 * blue);
        var m = (0.2119034982 * red) + (0.6806995451 * green) + (0.1073969566 * blue);
        var s = (0.0883024619 * red) + (0.2817188376 * green) + (0.6299787005 * blue);
        var lRoot = Math.Cbrt(l);
        var mRoot = Math.Cbrt(m);
        var sRoot = Math.Cbrt(s);
        return new PhotoStylingOklab(
            (0.2104542553 * lRoot) + (0.7936177850 * mRoot) - (0.0040720468 * sRoot),
            (1.9779984951 * lRoot) - (2.4285922050 * mRoot) + (0.4505937099 * sRoot),
            (0.0259040371 * lRoot) + (0.7827717662 * mRoot) - (0.8086757660 * sRoot));
    }

    public StageColor ToSrgb()
    {
        var lRoot = L + (0.3963377774 * A) + (0.2158037573 * B);
        var mRoot = L - (0.1055613458 * A) - (0.0638541728 * B);
        var sRoot = L - (0.0894841775 * A) - (1.2914855480 * B);
        var l = lRoot * lRoot * lRoot;
        var m = mRoot * mRoot * mRoot;
        var s = sRoot * sRoot * sRoot;
        var red = (+4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var green = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var blue = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);
        return new StageColor(ToByte(red), ToByte(green), ToByte(blue));
    }

    private static double ToLinear(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static byte ToByte(double linear)
    {
        var bounded = Math.Clamp(linear, 0, 1);
        var srgb = bounded <= 0.0031308
            ? 12.92 * bounded
            : (1.055 * Math.Pow(bounded, 1d / 2.4)) - 0.055;
        return (byte)Math.Clamp((int)Math.Round(srgb * 255), 0, byte.MaxValue);
    }
}
