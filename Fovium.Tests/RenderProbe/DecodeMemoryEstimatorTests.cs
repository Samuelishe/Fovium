using Fovium.RenderProbe;

namespace Fovium.Tests.RenderProbe;

public sealed class DecodeMemoryEstimatorTests
{
    [Fact]
    public void EstimatesNormalTwoRepresentationDecode()
    {
        var bytes = DecodeMemoryEstimator.EstimateBytes(4000, 3000);

        Assert.Equal(96_000_000, bytes);
        Assert.True(DecodeMemoryEstimator.IsWithinProbeCap(4000, 3000));
    }

    [Fact]
    public void RejectsLargeDecodeByDecodedCost()
    {
        var bytes = DecodeMemoryEstimator.EstimateBytes(30_000, 20_000);

        Assert.Equal(4_800_000_000, bytes);
        Assert.False(DecodeMemoryEstimator.IsWithinProbeCap(30_000, 20_000));
    }

    [Fact]
    public void CheckedArithmeticThrowsOnOverflow()
    {
        Assert.Throws<OverflowException>(() => DecodeMemoryEstimator.EstimateBytes(
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue));
    }
}
