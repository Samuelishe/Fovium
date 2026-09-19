using Fovium.ColorPicking;
using Fovium.Stage;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorSelectionModelTests
{
    [Theory]
    [InlineData(0, 1, 1, 255, 0, 0)]
    [InlineData(120, 1, 1, 0, 255, 0)]
    [InlineData(240, 1, 1, 0, 0, 255)]
    [InlineData(180, 1, 1, 0, 255, 255)]
    [InlineData(300, 1, 1, 255, 0, 255)]
    [InlineData(42, 0, 0, 0, 0, 0)]
    [InlineData(42, 0, 1, 255, 255, 255)]
    [InlineData(42, 0, 0.5, 128, 128, 128)]
    public void HsvToRgbProducesDeterministicReferenceBytes(
        double hue,
        double saturation,
        double value,
        byte red,
        byte green,
        byte blue)
    {
        Assert.Equal(
            new StageColor(red, green, blue),
            ColorSelectionModel.FromHsv(new HsvColor(hue, saturation, value)));
    }

    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(0, 255, 255, 180, 1, 1)]
    [InlineData(128, 128, 128, 0, 0, 128d / 255)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    public void RgbToHsvProducesExpectedPrimaryAndGrayscaleValues(
        byte red,
        byte green,
        byte blue,
        double hue,
        double saturation,
        double value)
    {
        var actual = ColorSelectionModel.ToHsv(new StageColor(red, green, blue));

        Assert.Equal(hue, actual.HueDegrees, 8);
        Assert.Equal(saturation, actual.Saturation, 8);
        Assert.Equal(value, actual.Value, 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(360)]
    [InlineData(720)]
    [InlineData(-360)]
    public void HueWrapKeepsZeroAndFullTurnsEquivalent(double hue)
    {
        Assert.Equal(
            new StageColor(255, 0, 0),
            ColorSelectionModel.FromHsv(new HsvColor(hue, 1, 1)));
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#FF0000")]
    [InlineData("#00FFFF")]
    [InlineData("#C28E73")]
    [InlineData("#8064C8")]
    public void ExactHexInputSurvivesModelRoundTripWithoutDrift(string hex)
    {
        var model = new ColorSelectionModel(default);

        Assert.True(model.TrySetHex(hex));
        Assert.Equal(hex, model.Hex);
        Assert.Equal(model.CurrentColor, ColorSelectionModel.FromHsv(model.Hsv));
    }

    [Fact]
    public void RgbAndHsvInputsSynchronizeEveryRepresentation()
    {
        var model = new ColorSelectionModel(default);

        Assert.True(model.TrySetRgb("29", "113", "150"));
        Assert.Equal("#1D7196", model.Hex);
        var hsv = model.Hsv;
        Assert.True(model.TrySetHsv(
            hsv.HueDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture),
            (hsv.Saturation * 100).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (hsv.Value * 100).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("#1D7196", model.Hex);
    }

    [Fact]
    public void CancelRestoresOriginalWhileAcceptCommitsCurrent()
    {
        var original = new StageColor(10, 20, 30);
        var changed = new StageColor(200, 100, 50);
        var model = new ColorSelectionModel(original);
        Assert.True(model.TrySetHex(changed.ToHex()));

        Assert.Equal(original, model.Resolve(ColorEditCompletion.Cancel));
        Assert.Equal(changed, model.Resolve(ColorEditCompletion.Accept));
    }

    [Fact]
    public void InvalidPreciseInputsLeaveTheExactCurrentColorUntouched()
    {
        var original = new StageColor(29, 113, 150);
        var model = new ColorSelectionModel(original);

        Assert.False(model.TrySetHex("#12345"));
        Assert.False(model.TrySetRgb("-1", "113", "150"));
        Assert.False(model.TrySetRgb("29", "256", "150"));
        Assert.False(model.TrySetHsv("20", "101", "50"));
        Assert.False(model.TrySetHsv("NaN", "50", "50"));
        Assert.Equal(original, model.CurrentColor);
        Assert.Equal("#1D7196", model.Hex);
    }
}