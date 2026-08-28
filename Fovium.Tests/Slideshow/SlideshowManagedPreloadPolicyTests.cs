using Fovium.Slideshow;

namespace Fovium.Tests.Slideshow;

public sealed class SlideshowManagedPreloadPolicyTests
{
    [Theory]
    [InlineData(15_000_000, true)]
    [InlineData(24_000_000, true)]
    [InlineData(50_000_000, false)]
    public void SpeculativeAdmissionUsesDecodedManagedBytes(long pixels, bool expected)
    {
        var bytes = checked(pixels * 4);

        var admitted = SlideshowManagedPreloadPolicy.IsAdmitted(0, bytes);

        Assert.Equal(expected, admitted);
    }

    [Fact]
    public void CurrentAndNextCannotExceedCombinedBudget()
    {
        var current24Mp = 24_000_000L * 4;
        var next24Mp = 24_000_000L * 4;
        var oversizedCurrent = 180_000_000L;

        Assert.True(SlideshowManagedPreloadPolicy.IsAdmitted(current24Mp, next24Mp));
        Assert.False(SlideshowManagedPreloadPolicy.IsAdmitted(oversizedCurrent, next24Mp));
        Assert.Equal(192_000_000L, current24Mp + next24Mp);
    }

    [Theory]
    [InlineData(0, SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes, true)]
    [InlineData(0, SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes + 1, false)]
    [InlineData(
        SlideshowManagedPreloadPolicy.MaximumCurrentAndNextManagedBytes -
        SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes,
        SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes,
        true)]
    [InlineData(
        SlideshowManagedPreloadPolicy.MaximumCurrentAndNextManagedBytes -
        SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes + 1,
        SlideshowManagedPreloadPolicy.MaximumSpeculativeManagedBytes,
        false)]
    [InlineData(-1, 1, false)]
    [InlineData(0, 0, false)]
    public void AdmissionBoundariesAreInclusiveAndRejectInvalidByteCounts(
        long currentBytes,
        long nextBytes,
        bool expected)
    {
        Assert.Equal(expected, SlideshowManagedPreloadPolicy.IsAdmitted(currentBytes, nextBytes));
    }
}
