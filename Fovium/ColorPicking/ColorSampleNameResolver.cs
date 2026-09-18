using Fovium.ColorSemantics;
using Fovium.Localization;

namespace Fovium.ColorPicking;

internal sealed class ColorSampleNameResolver(
    Localizer localizer,
    ColorNameDisplayCatalog colorNames)
{
    public string Resolve(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.IsTransparent || string.IsNullOrWhiteSpace(sample.CanonicalName))
        {
            return localizer[UiStrings.ColorPickerTransparent];
        }

        return colorNames.Resolve(sample.ColorNameStableId, sample.CanonicalName);
    }
}