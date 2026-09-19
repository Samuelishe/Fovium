using Fovium.Home;
using Fovium.Settings;

namespace Fovium.Tests.Home;

public sealed class RecentCapturePolicyTests
{
    [Fact]
    public void OpenedOnlyCapturesExplicitOpensAndIgnoresOrdinaryNavigation()
    {
        var settings = HomeSettings.Default with
        {
            CapturePolicy = RecentCapturePolicy.OpenedItemsOnly,
        };

        Assert.True(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.ExplicitOpen));
        Assert.False(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.ManualNavigation));
    }

    [Fact]
    public void EveryViewedCapturesSuccessfulManualNavigationOnly()
    {
        var settings = HomeSettings.Default with
        {
            CapturePolicy = RecentCapturePolicy.EveryViewedPhoto,
        };

        Assert.True(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.ManualNavigation));
        Assert.False(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.Preload));
        Assert.False(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.FailedOrStalePublication));
    }

    [Fact]
    public void SlideshowNeverFloodsRecentHistory()
    {
        var settings = HomeSettings.Default with
        {
            CapturePolicy = RecentCapturePolicy.EveryViewedPhoto,
        };

        Assert.False(RecentCapturePolicyResolver.ShouldCapture(
            settings,
            RecentCaptureTrigger.SlideshowNavigation));
    }

    [Theory]
    [InlineData((int)RecentCapturePolicy.OpenedItemsOnly)]
    [InlineData((int)RecentCapturePolicy.EveryViewedPhoto)]
    public void RememberRecentFalseOverridesEveryCapturePolicy(int policyValue)
    {
        var settings = HomeSettings.Default with
        {
            RememberRecentPhotos = false,
            CapturePolicy = (RecentCapturePolicy)policyValue,
        };

        foreach (var trigger in Enum.GetValues<RecentCaptureTrigger>())
        {
            Assert.False(RecentCapturePolicyResolver.ShouldCapture(settings, trigger));
        }
    }
}