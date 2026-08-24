using Fovium.Imaging;

namespace Fovium.Tests.Imaging;

public sealed class DecodeMemoryEstimatorTests
{
    [Fact]
    public void NormalImageIncludesEncodedAndPixelRepresentations()
    {
        Assert.Equal(96_500_000, DecodeMemoryEstimator.EstimateWorkingBytes(4000, 3000, 500_000));
        Assert.Equal(48_500_000, DecodeMemoryEstimator.EstimateRetainedBytes(4000, 3000, 500_000));
    }

    [Fact]
    public void LargeImageCostIsBasedOnDecodedDimensions()
    {
        var bytes = DecodeMemoryEstimator.EstimateWorkingBytes(30_000, 20_000, 1_000_000);

        Assert.Equal(4_801_000_000, bytes);
    }

    [Fact]
    public void CheckedArithmeticRejectsOverflow()
    {
        Assert.Throws<OverflowException>(() => DecodeMemoryEstimator.EstimateWorkingBytes(
            int.MaxValue,
            int.MaxValue,
            long.MaxValue));
    }
}
