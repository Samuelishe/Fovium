using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorPickerInteractionTests
{
    [Fact]
    public void ActivePickerOwnsOrdinaryPrimaryClickAheadOfMarkup()
    {
        var action = ColorPickerInteraction.ResolvePrimaryClick(
            pickerEnabled: true,
            temporaryHandActive: false);

        Assert.Equal(ColorPickerPrimaryClickAction.Sample, action);
    }

    [Fact]
    public void TemporaryHandOverridesPickerOnlyWhileHeld()
    {
        Assert.Equal(
            ColorPickerPrimaryClickAction.Pan,
            ColorPickerInteraction.ResolvePrimaryClick(true, true));
        Assert.Equal(
            ColorPickerPrimaryClickAction.Sample,
            ColorPickerInteraction.ResolvePrimaryClick(true, false));
    }

    [Fact]
    public void DisabledPickerLeavesExistingInteractionUntouched()
    {
        Assert.Equal(
            ColorPickerPrimaryClickAction.PassThrough,
            ColorPickerInteraction.ResolvePrimaryClick(false, false));
        Assert.Equal(
            ColorPickerPrimaryClickAction.PassThrough,
            ColorPickerInteraction.ResolvePrimaryClick(false, true));
    }
}
