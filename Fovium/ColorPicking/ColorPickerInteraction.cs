namespace Fovium.ColorPicking;

internal enum ColorPickerPrimaryClickAction
{
    PassThrough,
    Sample,
    Pan,
}

internal static class ColorPickerInteraction
{
    public static ColorPickerPrimaryClickAction ResolvePrimaryClick(
        bool pickerEnabled,
        bool temporaryHandActive)
    {
        if (!pickerEnabled)
        {
            return ColorPickerPrimaryClickAction.PassThrough;
        }

        return temporaryHandActive
            ? ColorPickerPrimaryClickAction.Pan
            : ColorPickerPrimaryClickAction.Sample;
    }
}
