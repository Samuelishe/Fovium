using Fovium.PhotoStyling;
using Fovium.Stage;
using Fovium.Tests.PhotoStyling;
using Fovium.Tests.Stage;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class PhotoDerivedStylingPresentationTests
{
    [Fact]
    public void NavigationPublishesOnlyAnalysisOwnedByActuallyPresentedImage()
    {
        var viewport = new PhotoViewportControl();
        var first = StageTestImages.CreateDecoded("A.png");
        var second = StageTestImages.CreateDecoded("B.png");
        var firstResource = new Fovium.Loading.SharedResource<Fovium.Imaging.DecodedImage>(first);
        var secondResource = new Fovium.Loading.SharedResource<Fovium.Imaging.DecodedImage>(second);
        AttachAnalysis(first, new StageColor(200, 20, 20));
        AttachAnalysis(second, new StageColor(20, 20, 200));
        try
        {
            using var firstPresentation = new StagePresentation(
                StageSettings.Default with { BackgroundMode = StageBackgroundMode.Average },
                first.Identity,
                null);
            viewport.SetPresentation(
                firstResource.Acquire(),
                Fovium.Rendering.ViewTransfer.Fit,
                "A.png",
                firstPresentation);
            var firstState = viewport.CapturePhotoStylePresentationState();

            using var secondPresentation = new StagePresentation(
                firstPresentation.Stage,
                second.Identity,
                null);
            viewport.SetPresentation(
                secondResource.Acquire(),
                Fovium.Rendering.ViewTransfer.Fit,
                "B.png",
                secondPresentation);
            var secondState = viewport.CapturePhotoStylePresentationState();

            Assert.Equal(first.Identity, firstState.ImageIdentity);
            Assert.Equal(first.Identity, firstState.PhotoStyleIdentity);
            Assert.Equal(second.Identity, secondState.ImageIdentity);
            Assert.Equal(second.Identity, secondState.PhotoStyleIdentity);
            Assert.NotEqual(firstState.PhotoStyleIdentity, secondState.PhotoStyleIdentity);
        }
        finally
        {
            viewport.ClearImage();
            firstResource.ReleaseOwner();
            secondResource.ReleaseOwner();
        }
    }

    [Fact]
    public void BlinkFollowsComparisonAnalysisAndPeekKeepsCanonicalAnalysis()
    {
        var viewport = new PhotoViewportControl();
        var current = StageTestImages.CreateDecoded("current.png");
        var comparison = StageTestImages.CreateDecoded("comparison.png");
        var currentResource = new Fovium.Loading.SharedResource<Fovium.Imaging.DecodedImage>(current);
        var comparisonResource = new Fovium.Loading.SharedResource<Fovium.Imaging.DecodedImage>(comparison);
        AttachAnalysis(current, new StageColor(220, 40, 30));
        AttachAnalysis(comparison, new StageColor(30, 60, 220));
        try
        {
            viewport.SetImage(
                currentResource.Acquire(),
                Fovium.Rendering.ViewTransfer.Fit,
                "current.png");
            Assert.True(viewport.BeginPeek100());
            Assert.Equal(current.Identity, viewport.CapturePhotoStylePresentationState().PhotoStyleIdentity);
            Assert.True(viewport.EndInspection());

            Assert.True(viewport.BeginBlinkCompare());
            Assert.True(viewport.ShowBlinkComparison(
                comparisonResource.Acquire(),
                "comparison.png",
                StageSettings.Default with { BackgroundMode = StageBackgroundMode.ColorWash },
                null));
            var comparisonState = viewport.CapturePhotoStylePresentationState();
            Assert.Equal(comparison.Identity, comparisonState.ImageIdentity);
            Assert.Equal(comparison.Identity, comparisonState.PhotoStyleIdentity);
            Assert.Equal(StageBackgroundMode.ColorWash, comparisonState.BackgroundMode);

            Assert.True(viewport.EndInspection());
            Assert.Equal(current.Identity, viewport.CapturePhotoStylePresentationState().PhotoStyleIdentity);
        }
        finally
        {
            viewport.ClearImage();
            currentResource.ReleaseOwner();
            comparisonResource.ReleaseOwner();
        }
    }

    private static void AttachAnalysis(Fovium.Imaging.DecodedImage image, StageColor color)
    {
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(color, color, color);
        if (!image.TryAttachPhotoStyleAnalysis(analysis))
        {
            throw new InvalidOperationException("Test analysis attachment failed.");
        }
    }
}
