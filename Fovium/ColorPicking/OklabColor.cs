namespace Fovium.ColorPicking;

internal readonly record struct OklabColor(double L, double A, double B)
{
    public static OklabColor FromSrgb(byte red, byte green, byte blue)
    {
        var linearRed = ToLinear(red / 255d);
        var linearGreen = ToLinear(green / 255d);
        var linearBlue = ToLinear(blue / 255d);

        var l = (0.4122214708 * linearRed) +
            (0.5363325363 * linearGreen) +
            (0.0514459929 * linearBlue);
        var m = (0.2119034982 * linearRed) +
            (0.6806995451 * linearGreen) +
            (0.1073969566 * linearBlue);
        var s = (0.0883024619 * linearRed) +
            (0.2817188376 * linearGreen) +
            (0.6299787005 * linearBlue);
        var lRoot = Math.Cbrt(l);
        var mRoot = Math.Cbrt(m);
        var sRoot = Math.Cbrt(s);

        return new OklabColor(
            (0.2104542553 * lRoot) + (0.7936177850 * mRoot) - (0.0040720468 * sRoot),
            (1.9779984951 * lRoot) - (2.4285922050 * mRoot) + (0.4505937099 * sRoot),
            (0.0259040371 * lRoot) + (0.7827717662 * mRoot) - (0.8086757660 * sRoot));
    }

    public double DistanceSquared(OklabColor other)
    {
        var deltaL = L - other.L;
        var deltaA = A - other.A;
        var deltaB = B - other.B;
        return (deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB);
    }

    private static double ToLinear(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
