using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class OklabColorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(255, 255, 255, 1, 0, 0)]
    [InlineData(255, 0, 0, 0.627955361, 0.224863061, 0.125846299)]
    [InlineData(0, 255, 0, 0.866439612, -0.233887574, 0.179498480)]
    [InlineData(0, 0, 255, 0.452013718, -0.032456984, -0.311528148)]
    public void SrgbPrimariesAndEndpointsMatchReferenceValues(
        byte red,
        byte green,
        byte blue,
        double expectedL,
        double expectedA,
        double expectedB)
    {
        var actual = OklabColor.FromSrgb(red, green, blue);

        Assert.Equal(expectedL, actual.L, 6);
        Assert.Equal(expectedA, actual.A, 6);
        Assert.Equal(expectedB, actual.B, 6);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(192)]
    public void NeutralSrgbValuesRemainOnNeutralAxis(byte value)
    {
        var actual = OklabColor.FromSrgb(value, value, value);

        Assert.True(double.IsFinite(actual.L));
        Assert.InRange(Math.Abs(actual.A), 0, 0.000001);
        Assert.InRange(Math.Abs(actual.B), 0, 0.000001);
    }
}
