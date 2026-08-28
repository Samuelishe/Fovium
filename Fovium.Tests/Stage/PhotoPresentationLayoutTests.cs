using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Stage;
using Xunit.Abstractions;

namespace Fovium.Tests.Stage;

public sealed class PhotoPresentationLayoutTests(ITestOutputHelper output)
{
    [Fact]
    public void CalculateBoundaryAcceptsOnlyPhotoSpatialInputs()
    {
        var calculate = Assert.Single(
            typeof(PhotoPresentationLayout).GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.DeclaredOnly),
            method => method.Name == nameof(PhotoPresentationLayout.Calculate));

        Assert.Equal(
            [typeof(LogicalSize), typeof(double), typeof(PixelSize), typeof(double)],
            calculate.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(PhotoPresentationLayoutResult), calculate.ReturnType);
    }

    public static TheoryData<int, int, double> AspectAndDpiCases
    {
        get
        {
            var result = new TheoryData<int, int, double>();
            (int Width, int Height)[] aspects =
            [
                (3, 2), (2, 3), (1, 1), (16, 9), (9, 16), (3, 1), (1, 3),
            ];
            double[] renderScalings = [1, 1.25, 1.5, 2];
            foreach (var aspect in aspects)
            {
                foreach (var renderScaling in renderScalings)
                {
                    result.Add(aspect.Width, aspect.Height, renderScaling);
                }
            }

            return result;
        }
    }

    public static TheoryData<bool, double, int, byte, byte, byte> MatteVariants
    {
        get
        {
            var result = new TheoryData<bool, double, int, byte, byte, byte>();
            bool[] enabledValues = [false, true];
            double[] widths =
            [
                1, 32, 64, 100, 147, StageDefaults.MatteWidthMaximumPhysicalPixels,
            ];
            StageColor[] colors =
            [
                new StageColor(0, 0, 0),
                new StageColor(32, 32, 32),
                new StageColor(245, 210, 120),
            ];

            foreach (var enabled in enabledValues)
            {
                foreach (var width in widths)
                {
                    foreach (var style in Enum.GetValues<MatteStyle>())
                    {
                        foreach (var color in colors)
                        {
                            result.Add(enabled, width, (int)style, color.Red, color.Green, color.Blue);
                        }
                    }
                }
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(AspectAndDpiCases))]
    public void PhysicalMarginContainsCenteredPhotoAcrossRequiredAspectsAndDpi(
        int aspectWidth,
        int aspectHeight,
        double renderScaling)
    {
        var viewport = new LogicalSize(1280, 800);
        var source = new PixelSize(aspectWidth * 4000, aspectHeight * 4000);

        var result = PhotoPresentationLayout.Calculate(
            viewport,
            renderScaling,
            source,
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent);

        var expectedPhysicalMargin =
            Math.Min(viewport.Width, viewport.Height) * renderScaling *
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent / 100;
        Assert.Equal(expectedPhysicalMargin, result.MarginPhysicalPixels, 9);
        Assert.Equal(
            source.Width / (double)source.Height,
            result.PhotoDestination.Width / result.PhotoDestination.Height,
            9);
        AssertCentered(result.PhotoDestination, viewport);
        AssertContains(result.PhotoPresentationBounds, result.PhotoDestination);
        Assert.True(IsTightOnAtLeastOneAxis(
            result.PhotoDestination,
            result.PhotoPresentationBounds));
        Assert.True(result.PhotoFitsPresentationBounds);
        AssertFinitePositive(result.PhotoDestination);
    }

    [Theory]
    [MemberData(nameof(MatteVariants))]
    public void MatteVariantsReuseExactStageIndependentPhotoLayout(
        bool matteEnabled,
        double matteWidth,
        int matteStyleValue,
        byte red,
        byte green,
        byte blue)
    {
        var viewport = new LogicalSize(1280, 800);
        const double renderScaling = 1.25;
        var source = new PixelSize(4000, 6000);
        const double edgeMarginPercent = 4;
        var rawStage = StageSettings.Default with
        {
            MatteEnabled = matteEnabled,
            MatteWidthPhysicalPixels = matteWidth,
            MatteStyle = (MatteStyle)matteStyleValue,
            MatteColor = new StageColor(red, green, blue),
        };

        var layout = PhotoPresentationLayout.Calculate(
            viewport,
            renderScaling,
            source,
            edgeMarginPercent);
        var viewportPhysicalWidth = viewport.Width * renderScaling;
        var viewportPhysicalHeight = viewport.Height * renderScaling;
        var expectedMarginPhysical =
            Math.Min(viewportPhysicalWidth, viewportPhysicalHeight) * edgeMarginPercent / 100;
        var expectedPhysicalScale = Math.Min(
            1,
            Math.Min(
                (viewportPhysicalWidth - (2 * expectedMarginPhysical)) / source.Width,
                (viewportPhysicalHeight - (2 * expectedMarginPhysical)) / source.Height));
        var stage = rawStage.Normalize();
        var renderGeometry = StageGeometry.CalculateRenderGeometry(
            stage,
            layout.PhotoDestination,
            ambientSize: null,
            viewport,
            renderScaling);

        Assert.Equal(expectedPhysicalScale, layout.PhysicalScale);
        Assert.Equal(layout.PhotoDestination, renderGeometry.PhotoDestination);
        if (matteWidth == 1)
        {
            Assert.Equal(
                StageDefaults.MatteWidthMinimumPhysicalPixels,
                stage.MatteWidthPhysicalPixels);
        }

        if (matteEnabled)
        {
            var matte = Assert.IsType<MatteRenderGeometry>(renderGeometry.Matte);
            Assert.Equal(layout.PhotoDestination, matte.BackingDestination);
            Assert.Equal((MatteStyle)matteStyleValue, matte.Style);
        }
        else
        {
            Assert.Null(renderGeometry.Matte);
        }
    }

    [Fact]
    public void PortraitOwnerVideoWidthSequenceChangesOnlyOuterGeometry()
    {
        var viewport = new LogicalSize(1280, 800);
        const double renderScaling = 1.25;
        var source = new PixelSize(4000, 6000);
        double[] widths = [32, 64, 100, 147, 32];

        var frames = widths
            .Select(width =>
            {
                var layout = PhotoPresentationLayout.Calculate(viewport, renderScaling, source, 4);
                var geometry = StageGeometry.CalculateRenderGeometry(
                    StageSettings.Default with
                    {
                        MatteEnabled = true,
                        MatteWidthPhysicalPixels = width,
                    },
                    layout.PhotoDestination,
                    ambientSize: null,
                    viewport,
                    renderScaling);
                return (Layout: layout, Geometry: geometry);
            })
            .ToArray();
        var outerBounds = frames
            .Select(frame => Assert.IsType<MatteRenderGeometry>(frame.Geometry.Matte).OuterBounds)
            .ToArray();

        Assert.All(frames.Skip(1), frame => Assert.Equal(frames[0].Layout, frame.Layout));
        Assert.All(frames, frame => Assert.Equal(
            frame.Layout.PhotoDestination,
            frame.Geometry.PhotoDestination));
        Assert.NotEqual(outerBounds[0], outerBounds[1]);
        Assert.NotEqual(outerBounds[1], outerBounds[2]);
        Assert.NotEqual(outerBounds[2], outerBounds[3]);
        Assert.NotEqual(outerBounds[3], outerBounds[4]);
        Assert.Equal(outerBounds[0], outerBounds[4]);
    }

    [Fact]
    public void MatteToggleChangesOuterGeometryWithoutMovingPhoto()
    {
        var viewport = new LogicalSize(1280, 800);
        const double renderScaling = 1.25;
        var layout = PhotoPresentationLayout.Calculate(
            viewport,
            renderScaling,
            new PixelSize(4000, 6000),
            4);
        var off = StageGeometry.CalculateRenderGeometry(
            StageSettings.Default with { MatteEnabled = false },
            layout.PhotoDestination,
            ambientSize: null,
            viewport,
            renderScaling);
        var on = StageGeometry.CalculateRenderGeometry(
            StageSettings.Default with
            {
                MatteEnabled = true,
                MatteWidthPhysicalPixels = 147,
            },
            layout.PhotoDestination,
            ambientSize: null,
            viewport,
            renderScaling);

        Assert.Equal(layout.PhotoDestination, off.PhotoDestination);
        Assert.Equal(layout.PhotoDestination, on.PhotoDestination);
        Assert.Null(off.Matte);
        var matte = Assert.IsType<MatteRenderGeometry>(on.Matte);
        Assert.NotEqual(layout.PhotoDestination, matte.OuterBounds);
    }

    [Fact]
    public void EdgeMarginChangesPhotoDestination()
    {
        var viewport = new LogicalSize(1280, 800);
        var source = new PixelSize(6000, 4000);
        var narrowMargin = PhotoPresentationLayout.Calculate(viewport, 1.25, source, 4);
        var wideMargin = PhotoPresentationLayout.Calculate(viewport, 1.25, source, 11);

        Assert.NotEqual(narrowMargin.PhotoDestination, wideMargin.PhotoDestination);
        Assert.NotEqual(narrowMargin.PhysicalScale, wideMargin.PhysicalScale);
        AssertCentered(narrowMargin.PhotoDestination, viewport);
        AssertCentered(wideMargin.PhotoDestination, viewport);
        Assert.Equal(
            source.Width / (double)source.Height,
            wideMargin.PhotoDestination.Width / wideMargin.PhotoDestination.Height,
            9);
    }

    [Theory]
    [InlineData(2400, 3600, 6000, 4000)]
    [InlineData(6000, 4000, 2400, 3600)]
    [InlineData(5000, 5000, 9000, 2000)]
    [InlineData(9000, 2000, 5000, 5000)]
    public void EveryOrientedShapeFitsIndependentlyOfPriorShape(
        int width,
        int height,
        int distractorWidth,
        int distractorHeight)
    {
        var viewport = new LogicalSize(1280, 800);
        var source = new PixelSize(width, height);
        var first = PhotoPresentationLayout.Calculate(viewport, 1.5, source, 5);

        _ = PhotoPresentationLayout.Calculate(
            viewport,
            1.5,
            new PixelSize(distractorWidth, distractorHeight),
            5);
        var afterDistractor = PhotoPresentationLayout.Calculate(viewport, 1.5, source, 5);

        Assert.Equal(first, afterDistractor);
        AssertCentered(first.PhotoDestination, viewport);
        Assert.Equal(
            width / (double)height,
            first.PhotoDestination.Width / first.PhotoDestination.Height,
            9);
        Assert.True(first.PhysicalScale > 0);
        Assert.True(first.PhotoFitsPresentationBounds);
    }

    [Fact]
    public void MatteOnlyChangesPreservePickerMarkupAndSourceMapping()
    {
        var viewport = new LogicalSize(1280, 800);
        const double renderScaling = 1.25;
        var source = new PixelSize(4000, 6000);
        var layout = PhotoPresentationLayout.Calculate(viewport, renderScaling, source, 4);
        var matteOff = StageGeometry.CalculateRenderGeometry(
            StageSettings.Default with { MatteEnabled = false },
            layout.PhotoDestination,
            ambientSize: null,
            viewport,
            renderScaling);
        var matteChanged = StageGeometry.CalculateRenderGeometry(
            StageSettings.Default with
            {
                MatteEnabled = true,
                MatteWidthPhysicalPixels = StageDefaults.MatteWidthMaximumPhysicalPixels,
                MatteStyle = MatteStyle.Angular,
                MatteColor = new StageColor(245, 210, 120),
            },
            layout.PhotoDestination,
            ambientSize: null,
            viewport,
            renderScaling);
        var sourcePoint = new PointD(1377.25, 2111.75);
        var offMarkupPoint = new MarkupTransform(matteOff.PhotoDestination, source)
            .SourceToViewport(sourcePoint);
        var changedMarkupPoint = new MarkupTransform(matteChanged.PhotoDestination, source)
            .SourceToViewport(sourcePoint);

        var offMapped = PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            matteOff.PhotoDestination,
            source,
            offMarkupPoint,
            out var offPixel);
        var changedMapped = PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            matteChanged.PhotoDestination,
            source,
            changedMarkupPoint,
            out var changedPixel);

        Assert.Equal(matteOff.PhotoDestination, matteChanged.PhotoDestination);
        Assert.Equal(offMarkupPoint, changedMarkupPoint);
        Assert.True(offMapped);
        Assert.True(changedMapped);
        Assert.Equal(offPixel, changedPixel);
        Assert.Equal(new PixelPoint(1377, 2111), changedPixel);
    }

    [Fact]
    public void ResizeAndFullscreenLikeViewportChangesLegitimatelyRecomputePhotoLayout()
    {
        var source = new PixelSize(6000, 4000);
        var windowedViewport = new LogicalSize(1280, 800);
        var resizedViewport = new LogicalSize(1400, 900);
        var fullscreenViewport = new LogicalSize(1920, 1080);
        var windowed = PhotoPresentationLayout.Calculate(windowedViewport, 1.25, source, 4);
        var resized = PhotoPresentationLayout.Calculate(resizedViewport, 1.25, source, 4);
        var fullscreen = PhotoPresentationLayout.Calculate(fullscreenViewport, 1.25, source, 4);

        Assert.NotEqual(windowed.PhotoDestination, resized.PhotoDestination);
        Assert.NotEqual(resized.PhotoDestination, fullscreen.PhotoDestination);
        AssertCentered(windowed.PhotoDestination, windowedViewport);
        AssertCentered(resized.PhotoDestination, resizedViewport);
        AssertCentered(fullscreen.PhotoDestination, fullscreenViewport);
        Assert.Equal(
            source.Width / (double)source.Height,
            fullscreen.PhotoDestination.Width / fullscreen.PhotoDestination.Height,
            9);
    }

    [Fact]
    public void TinyViewportDegradesDeterministicallyWithoutInvalidGeometry()
    {
        var result = PhotoPresentationLayout.Calculate(
            new LogicalSize(0.25, 0.25),
            1,
            new PixelSize(24_000, 16_000),
            PhotoPresentationViewSettings.MaximumEdgeMarginPercent);

        Assert.Equal(0, result.MarginPhysicalPixels);
        Assert.False(result.PhotoFitsPresentationBounds);
        AssertFinitePositive(result.PhotoDestination);
        AssertFinitePositive(result.PhotoPresentationBounds);
        Assert.True(result.PhysicalScale > 0);
        Assert.True(double.IsFinite(result.PhysicalScale));
    }

    [Fact]
    public void InvalidSpatialInputsAreRejectedInsteadOfProducingNaNLayout()
    {
        var viewport = new LogicalSize(1200, 800);
        var source = new PixelSize(6000, 4000);

        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            new LogicalSize(0, 800), 1, source, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, double.NaN, source, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, new PixelSize(0, 4000), 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, source, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoPresentationLayout.Calculate(
            viewport, 1, source, 15.01));
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
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent);

        Assert.Equal(1, result.PhysicalScale);
        Assert.True(result.UsesExactPixelSampling);
        Assert.Equal(320 / renderScaling, result.PhotoDestination.Width, 9);
        Assert.Equal(200 / renderScaling, result.PhotoDestination.Height, 9);
    }

    [Fact]
    public void RetainedBlinkPhotoUsesIndependentGeometryWithoutChangingCanonicalLayout()
    {
        var viewport = new LogicalSize(1200, 800);
        var canonicalBefore = PhotoPresentationLayout.Calculate(
            viewport,
            1.25,
            new PixelSize(6000, 4000),
            4);
        var retainedBlink = PhotoPresentationLayout.Calculate(
            viewport,
            1.25,
            new PixelSize(2400, 3600),
            4);
        var canonicalAfter = PhotoPresentationLayout.Calculate(
            viewport,
            1.25,
            new PixelSize(6000, 4000),
            4);

        Assert.NotEqual(canonicalBefore.PhotoDestination, retainedBlink.PhotoDestination);
        AssertContains(retainedBlink.PhotoPresentationBounds, retainedBlink.PhotoDestination);
        Assert.Equal(canonicalBefore, canonicalAfter);
    }

    [Fact]
    public void RepeatedLayoutCalculationIsObservedWithoutDefiningAnSla()
    {
        const int attempts = 100_000;
        var viewport = new LogicalSize(2560, 1440);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        PhotoPresentationLayoutResult result = default;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            result = PhotoPresentationLayout.Calculate(
                viewport,
                1.5,
                new PixelSize(6000, 4000),
                4);
        }

        stopwatch.Stop();
        Assert.True(result.PhotoFitsPresentationBounds);
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
