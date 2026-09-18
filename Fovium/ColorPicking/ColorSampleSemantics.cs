using Fovium.ColorSemantics;

namespace Fovium.ColorPicking;

internal static class ColorSampleSemantics
{
    public static PerceptualColorDescription Describe(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return sample.IsTransparent
            ? PerceptualColorDescription.Transparent
            : PerceptualColorClassifier.Describe(sample.Red, sample.Green, sample.Blue);
    }
}