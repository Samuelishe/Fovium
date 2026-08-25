using Fovium.Presentation;

namespace Fovium.Tests.Presentation;

public sealed class InteractionRenderRoutingTests
{
    [Fact]
    public void PointerPositionChangedDirtiesOnlyPointerFeedback()
    {
        Assert.Equal(
            InteractionRenderLayer.Pointer,
            InteractionRenderRouting.ForPointerPosition());
    }

    [Fact]
    public void DraftContentChangedDirtiesOnlyMarkup()
    {
        Assert.Equal(
            InteractionRenderLayer.Markup,
            InteractionRenderRouting.ForPresentationChange(
                PresentationChangeKind.RenderContent));
    }

    [Fact]
    public void DockPositionChangedUsesOnlyFloatingUiTransform()
    {
        Assert.Equal(
            InteractionRenderLayer.FloatingUi,
            InteractionRenderRouting.ForDockPosition());
    }

    [Fact]
    public void ViewportChangedDirtiesPhotoAndMarkupButNotPointerOrToolbar()
    {
        Assert.Equal(
            InteractionRenderLayer.Photo | InteractionRenderLayer.Markup,
            InteractionRenderRouting.ForViewportChange());
    }

    [Fact]
    public void StageChangedDirtiesOnlyPhoto()
    {
        Assert.Equal(
            InteractionRenderLayer.Photo,
            InteractionRenderRouting.ForStageChange());
    }

    [Fact]
    public void StyleChangedUpdatesPointerAndToolbarWithoutPhotoOrMarkupReplay()
    {
        Assert.Equal(
            InteractionRenderLayer.Pointer | InteractionRenderLayer.Toolbar,
            InteractionRenderRouting.ForPresentationChange(
                PresentationChangeKind.StyleState));
    }
}
