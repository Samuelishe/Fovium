using Fovium.ColorManagementProbe;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class ColorFallbackClassifierTests
{
    [Theory]
    [InlineData(false, true, true, true, (int)DisplayColorFallback.PlatformUnsupported)]
    [InlineData(true, false, true, true, (int)DisplayColorFallback.UnsupportedDisplayMode)]
    [InlineData(true, true, false, true, (int)DisplayColorFallback.InvalidDestinationProfile)]
    [InlineData(true, true, true, false, (int)DisplayColorFallback.UnsupportedSourceProfile)]
    [InlineData(true, true, true, true, (int)DisplayColorFallback.Managed)]
    public void ClassifierRetainsExplicitFallbackCauseAndPrecedence(
        bool platformSupported,
        bool displayModeSupported,
        bool validDestination,
        bool sourceTransformSupported,
        int expectedValue)
    {
        var destination = validDestination ? CreateValidInspection() : CreateInvalidInspection();

        var actual = ColorFallbackClassifier.Classify(
            platformSupported,
            displayModeSupported,
            destination,
            sourceTransformSupported);

        Assert.Equal((DisplayColorFallback)expectedValue, actual);
    }

    [Fact]
    public void MissingDestinationIsDistinctFromInvalidDestination()
    {
        var missing = ColorFallbackClassifier.Classify(true, true, null, true);
        var invalid = ColorFallbackClassifier.Classify(true, true, CreateInvalidInspection(), true);

        Assert.Equal(DisplayColorFallback.DestinationUnavailable, missing);
        Assert.Equal(DisplayColorFallback.InvalidDestinationProfile, invalid);
        Assert.NotEqual(missing, invalid);
    }

    private static IccProfileInspection CreateValidInspection()
    {
        var bytes = IccTestData.CreateMinimalProfile();
        return IccProfileInspector.Inspect(bytes);
    }

    private static IccProfileInspection CreateInvalidInspection() =>
        IccProfileInspector.Inspect([]);
}
