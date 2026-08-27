using Fovium.ColorManagement;
using Fovium.Imaging;

namespace Fovium.Tests.ColorManagement;

public sealed class MonitorColorPolicyTests
{
    private static readonly DisplayProfileResolution ManagedDestination = new(
        MonitorColorState.Managed,
        new DisplayProfile(
            DisplayIccProfileAdmissionTests.CreateProfileHeader(),
            new DisplayProfileIdentity("ABCDEF", false),
            "Synthetic",
            false,
            "monitor",
            1),
        "managed");

    [Theory]
    [InlineData((int)SourceColorState.AssumedSrgb)]
    [InlineData((int)SourceColorState.NormalizedSrgb)]
    [InlineData((int)SourceColorState.NormalizedSrgbFromNclx)]
    [InlineData((int)SourceColorState.NormalizedNonSrgb)]
    public void EligibleSourcesAreManaged(int sourceStateValue)
    {
        var sourceState = (SourceColorState)sourceStateValue;
        var state = MonitorColorPolicy.Classify(true, true, true, ManagedDestination, sourceState);

        Assert.Equal(MonitorColorState.Managed, state);
    }

    [Fact]
    public void UnpreservedEmbeddedProfileRemainsApproximateFallback()
    {
        var state = MonitorColorPolicy.Classify(
            true,
            true,
            true,
            ManagedDestination,
            SourceColorState.EmbeddedProfileUnpreserved);

        Assert.Equal(MonitorColorState.UnsupportedSourceProfile, state);
    }

    [Theory]
    [InlineData(false, true, true, (int)MonitorColorState.Disabled)]
    [InlineData(true, false, true, (int)MonitorColorState.PlatformUnsupported)]
    [InlineData(true, true, false, (int)MonitorColorState.EngineUnavailable)]
    public void OuterFallbacksPrecedeSourceClassification(
        bool enabled,
        bool platformSupported,
        bool engineAvailable,
        int expectedValue)
    {
        var expected = (MonitorColorState)expectedValue;
        var state = MonitorColorPolicy.Classify(
            enabled,
            platformSupported,
            engineAvailable,
            ManagedDestination,
            SourceColorState.AssumedSrgb);

        Assert.Equal(expected, state);
    }

    [Fact]
    public void AdvancedColorFallbackNeverInvokesManagedSourceClassification()
    {
        var destination = new DisplayProfileResolution(
            MonitorColorState.UnsupportedDisplayMode,
            null,
            "Advanced Color enabled",
            true);

        var state = MonitorColorPolicy.Classify(
            true,
            true,
            true,
            destination,
            SourceColorState.AssumedSrgb);

        Assert.Equal(MonitorColorState.UnsupportedDisplayMode, state);
    }
}
