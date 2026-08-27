using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Stage;
using Xunit.Abstractions;

namespace Fovium.Tests.Stage;

public sealed class PhotoPresentationLayoutTests(ITestOutputHelper output)
{
    public static TheoryData<int, int, double, bool, int> AspectDpiAndMatteCases
    {
        get
        {
            var result = new TheoryData<int, int, double, bool, int>();
            (int Width, int Height)[] aspects =
            [
                (3, 2),
                (2, 3),
                (1, 1),
                (16, 9),
                (9, 16),
                (3, 1),
                (1, 3),
            ];
            double[] renderScalings = [1, 1.25, 1.5, 2];
            (bool Enabled, MatteStyle Style)[] matteCases =
            [
                (false, MatteStyle.Solid),
                (true, MatteStyle.Solid),
                (true, MatteStyle.Rounded),
                (true, MatteStyle.Soft),
                (true, MatteStyle.Angular),
            ];
            foreach (var aspect in aspects)
            {
                foreach (var renderScaling in renderScalings)
                {
                    foreach (var matte in matteCases)
                    {
                        result.Add(
                            aspect.Width,
                            aspect.Height,
                            renderScaling,
                            matte.Enabled,
                            (int)matte.Style);
                    }
                }
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(AspectDpiAndMatteCases))]
    public void PhysicalMarginContainsCenteredPhotoAcrossRequiredAspectsDpiAndMatte(
        int aspectWidth,
        int aspectHeight,
        double renderScaling,
        bool matteEnabled,
        int matteStyleValue)
    {
        var viewport = new LogicalSize(1280, 800);
        var source = new PixelSize(aspectWidth * 4000, aspectHeight * 4000);
        var stage = StageSettings.Default with
        {
            MatteEnabled = matteEnabled,
            MatteStyle = (MatteStyle)matteStyleValue,
            MatteWidthPhysicalPixels = 96,
        };

        var result = PhotoPresentationLayout.Calculate(
            viewport,
            renderScaling,
            source,
            stage,
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent);

        var expectedPhysicalMargin =
            Math.Min(viewport.Width, viewport.Height) * renderScaling *
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent / 100;
        Assert.Equal(expectedPhysicalMargin, result.MarginPhysicalPixels, 9);
        Assert.Equal(source.Width / (double)source.Height,
            result.PhotoDestination.Width / result.PhotoDestination.Height, 9);
        AssertCentered(result.PhotoDestination, viewport);
        AssertContains(result.PresentationBounds, result.OuterPresentationBounds);
        Assert.True(IsTightOnAtLeastOneAxis(result.OuterPresentationBounds, result.PresentationBounds));
        Assert.True(result.FitsRequestedBounds);
        AssertFinitePositive(result.PhotoDestination);
    }

    [Theory]
    [InlineData(false, (int)MatteStyle.Solid)]
    [InlineData(true, (int)MatteStyle.Solid)]
    [InlineData(true, (int)MatteStyle.Rounded)]
    [InlineData(true, (int)MatteStyle.Soft)]
    [InlineData(true, (int)MatteStyle.Angular)]
    public void MatteOffAndEveryStyleFitCanonicalOuterGeometry(
        bool matteEnabled,
        int matteStyleValue)
    {
        const double renderScaling = 1.5;
        const double physicalMatteWidth = 96;
        var viewport = new LogicalSize(900, 600);
        var stage = StageSettings.Default with
        {
            MatteEnabled = matteEnabled,
            MatteStyle = (MatteStyle)matteStyleValue,
            MatteWidthPhysicalPixels = physicalMatteWidth,
        };

        var result = PhotoPresentationLayout.Calculate(
            viewport,
            renderScaling,
            new PixelSize(6000, 4000),
            stage,
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent);

        Assert.True(result.FitsRequestedBounds);
        AssertContains(result.PresentationBounds, result.OuterPresentationBounds);
        if (matteEnabled)
        {
            var widthDip = physicalMatteWidth / renderScaling;
            Assert.Equal(result.PhotoDestination.X - widthDip, result.OuterPresentationBounds.X, 9);
            Assert.Equal(result.PhotoDestination.Y - widthDip, result.OuterPresentationBounds.Y, 9);
            Assert.Equal(result.PhotoDestination.Width + (2 * widthDip), result.OuterPresentationBounds.Width, 9);
            Assert.Equal(result.PhotoDestination.Height + (2 * widthDip), result.OuterPresentationBounds.Height, 9);
        }
        else
        {
            Assert.Equal(result.PhotoDestination, result.OuterPresentationBounds);
        }
    }

    [Fact]
    public void MatteStyleChangesRenderingShapeButNotCanonicalFit()
    {
        var results = Enum.GetValues<MatteStyle>()
            .Select(style => PhotoPresentationLayout.Calculate(
                new LogicalSize(1100, 700),
                1.25,
                new PixelSize(7000, 3000),
                StageSettings.Default with
                {
                    MatteEnabled = true,
                    MatteStyle = style,
                    MatteWidthPhysicalPixels = 128,
                },
                7))
            .ToArray();

        Assert.All(results, result => Assert.True(result.FitsRequestedBounds));
        Assert.All(results.Skip(1), result =>
        {
            Assert.Equal(results[0].PhotoDestination, result.PhotoDestination);
            Assert.Equal(results[0].OuterPresentationBounds, result.OuterPresentationBounds);
            Assert.Equal(results[0].PresentationBounds, result.PresentationBounds);
        });
    }

    [Fact]
    public void VerySmallViewportAndMaximumMatteDegradeDeterministicallyWithoutInvalidGeometry()
    {
        var viewport = new LogicalSize(12, 9);

        var result = PhotoPresentationLayout.Calculate(
            viewport,
            2,
            new PixelSize(24_000, 16_000),
            StageSettings.Default with
            {
                MatteEnabled = true,
                MatteStyle = MatteStyle.Soft,
                MatteWidthPhysicalPixels = StageDefaults.MatteWidthMaximumPhysicalPixels,
            },
            PhotoPresentationViewSettings.MaximumEdgeMarginPercent);

        Assert.Equal(0, result.MarginPhysicalPixels);
        Assert.False(result.FitsRequestedBounds);
        AssertFinitePositive(result.PhotoDestination);
        AssertFinitePositive(result.OuterPresentationBounds);
        Assert.True(result.PhysicalScale > 0);
        Assert.True(double.IsFinite(result.PhysicalScale));
    }

    [Fact]
    public void InvalidSpatialInputsAreRejectedInsteadOfProducingNaNLayout()
    {
        var viewport = new LogicalSize(1200, 800);
        var source = new PixelSize(6000, 4000);

        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            new LogicalSize(0, 800), 1, source, StageSettings.Default, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, double.NaN, source, StageSettings.Default, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, new PixelSize(0, 4000), StageSettings.Default, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, source, StageSettings.Default, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, source, StageSettings.Default, 15.01));
        Assert.Throws<ArgumentNullException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, source, null!, 4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void SmallPhotoNeverUpscalesBeyondPhotographic100(double renderScaling)
    {
        var result = PhotoPresentationLayout.Calculate(
            new LogicalSize(1920, 1080),
            renderScaling,
            new PixelSize(320, 200),
            StageSettings.Default,
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent);

        Assert.Equal(1, result.PhysicalScale);
        Assert.True(result.UsesExactPixelSampling);
        Assert.Equal(320 / renderScaling, result.PhotoDestination.Width, 9);
        Assert.Equal(200 / renderScaling, result.PhotoDestination.Height, 9);
    }

    [Fact]
    public void NavigationLayoutIsIndependentOfPreviousPhotoGeometry()
    {
        var viewport = new LogicalSize(1200, 800);
        var stage = StageSettings.Default with { MatteEnabled = true, MatteWidthPhysicalPixels = 48 };
        var portrait = new PixelSize(2400, 3600);
        var landscape = new PixelSize(6000, 2000);

        var portraitFirst = PhotoPresentationLayout.Calculate(viewport, 1.5, portrait, stage, 5);
        var landscapeSecond = PhotoPresentationLayout.Calculate(viewport, 1.5, landscape, stage, 5);
        var landscapeFirst = PhotoPresentationLayout.Calculate(viewport, 1.5, landscape, stage, 5);
        var portraitSecond = PhotoPresentationLayout.Calculate(viewport, 1.5, portrait, stage, 5);

        Assert.Equal(portraitFirst, portraitSecond);
        Assert.Equal(landscapeFirst, landscapeSecond);
        Assert.NotEqual(portraitFirst.PhotoDestination, landscapeFirst.PhotoDestination);
    }

    [Fact]
    public void MarginMatteResizeAndFullscreenEachRecomputeFromCurrentSpatialInputs()
    {
        var source = new PixelSize(6000, 4000);
        var baseStage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteStyle = MatteStyle.Solid,
            MatteWidthPhysicalPixels = 48,
        };
        var initial = PhotoPresentationLayout.Calculate(
            new LogicalSize(1280, 800), 1.25, source, baseStage, 4);
        var marginChanged = PhotoPresentationLayout.Calculate(
            new LogicalSize(1280, 800), 1.25, source, baseStage, 11);
        var matteChanged = PhotoPresentationLayout.Calculate(
            new LogicalSize(1280, 800),
            1.25,
            source,
            baseStage with { MatteStyle = MatteStyle.Angular, MatteWidthPhysicalPixels = 120 },
            11);
        var resized = PhotoPresentationLayout.Calculate(
            new LogicalSize(1400, 900),
            1.25,
            source,
            baseStage with { MatteStyle = MatteStyle.Angular, MatteWidthPhysicalPixels = 120 },
            11);
        var fullscreen = PhotoPresentationLayout.Calculate(
            new LogicalSize(1920, 1080),
            1.25,
            source,
            baseStage with { MatteStyle = MatteStyle.Angular, MatteWidthPhysicalPixels = 120 },
            11);

        Assert.NotEqual(initial.PresentationBounds, marginChanged.PresentationBounds);
        Assert.NotEqual(marginChanged.OuterPresentationBounds, matteChanged.OuterPresentationBounds);
        Assert.NotEqual(matteChanged.PresentationBounds, resized.PresentationBounds);
        Assert.NotEqual(resized.PresentationBounds, fullscreen.PresentationBounds);
        Assert.Equal(source.Width / (double)source.Height,
            fullscreen.PhotoDestination.Width / fullscreen.PhotoDestination.Height, 9);
        AssertContains(fullscreen.PresentationBounds, fullscreen.OuterPresentationBounds);
    }

    [Fact]
    public void RetainedBlinkPhotoUsesIndependentGeometryWithoutChangingCanonicalLayout()
    {
        var viewport = new LogicalSize(1200, 800);
        var stage = StageSettings.Default with { MatteEnabled = true, MatteWidthPhysicalPixels = 40 };
        var canonicalBefore = PhotoPresentationLayout.Calculate(
            viewport, 1.25, new PixelSize(6000, 4000), stage, 4);

        var retainedBlink = PhotoPresentationLayout.Calculate(
            viewport, 1.25, new PixelSize(2400, 3600), stage, 4);
        var canonicalAfter = PhotoPresentationLayout.Calculate(
            viewport, 1.25, new PixelSize(6000, 4000), stage, 4);

        Assert.NotEqual(canonicalBefore.PhotoDestination, retainedBlink.PhotoDestination);
        AssertContains(retainedBlink.PresentationBounds, retainedBlink.OuterPresentationBounds);
        Assert.Equal(canonicalBefore, canonicalAfter);
    }

    [Fact]
    public void RepeatedLayoutCalculationIsObservedWithoutDefiningAnSla()
    {
        const int attempts = 100_000;
        var viewport = new LogicalSize(2560, 1440);
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteStyle = MatteStyle.Soft,
            MatteWidthPhysicalPixels = 64,
        };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        PhotoPresentationLayoutResult result = default;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            result = PhotoPresentationLayout.Calculate(
                viewport,
                1.5,
                new PixelSize(6000, 4000),
                stage,
                4);
        }

        stopwatch.Stop();
        Assert.True(result.FitsRequestedBounds);
        Assert.True(result.PhotoDestination.Width > 0);
        output.WriteLine(
            $"Photo Presentation layout: {attempts:N0} calculations in " +
            $"{stopwatch.Elapsed.TotalMilliseconds:F2} ms " +
            $"({stopwatch.Elapsed.TotalNanoseconds / attempts:F1} ns/calculation).");
    }

    private static void AssertCentered(RectD bounds, LogicalSize viewport)
    {
        Assert.Equal(viewport.Width / 2, bounds.X + (bounds.Width / 2), 9);
        Assert.Equal(viewport.Height / 2, bounds.Y + (bounds.Height / 2), 9);
    }

    private static void AssertContains(RectD outer, RectD inner)
    {
        Assert.True(inner.X >= outer.X - 1e-9);
        Assert.True(inner.Y >= outer.Y - 1e-9);
        Assert.True(inner.X + inner.Width <= outer.X + outer.Width + 1e-9);
        Assert.True(inner.Y + inner.Height <= outer.Y + outer.Height + 1e-9);
    }

    private static bool IsTightOnAtLeastOneAxis(RectD inner, RectD outer) =>
        Math.Abs(inner.Width - outer.Width) <= 1e-9 ||
        Math.Abs(inner.Height - outer.Height) <= 1e-9;

    private static void AssertFinitePositive(RectD value)
    {
        Assert.True(double.IsFinite(value.X));
        Assert.True(double.IsFinite(value.Y));
        Assert.True(double.IsFinite(value.Width));
        Assert.True(double.IsFinite(value.Height));
        Assert.True(value.Width > 0);
        Assert.True(value.Height > 0);
    }
}
