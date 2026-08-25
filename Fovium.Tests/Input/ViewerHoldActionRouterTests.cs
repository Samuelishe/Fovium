using Fovium.Input;
using Fovium.Presentation;

namespace Fovium.Tests.Input;

public sealed class ViewerHoldActionRouterTests
{
    [Fact]
    public async Task TemporarySpaceHandUsesSharedRepeatReleaseAndFocusSafety()
    {
        var session = new PresentationOverlaySession(PresentationSettings.Default);
        session.SelectImage("A");
        session.ToggleMarkupTools();
        session.SetActiveTool(MarkupTool.Brush);
        var inspection = new RecordingHoldAction();
        var controller = new ViewerHoldController(new ViewerHoldActionRouter(
            inspection,
            new MarkupTemporaryHandHoldAction(session)));

        Assert.True(await controller.TryBeginAsync(
            ViewerCommand.TemporaryMarkupHand,
            "Space",
            CancellationToken.None));
        Assert.False(await controller.TryBeginAsync(
            ViewerCommand.TemporaryMarkupHand,
            "Space",
            CancellationToken.None));
        Assert.Equal(MarkupTool.Hand, session.EffectiveTool);
        Assert.Equal(MarkupTool.Brush, session.ActiveTool);
        Assert.True(controller.EndPrimaryKey("Space"));
        Assert.Equal(MarkupTool.Brush, session.EffectiveTool);

        Assert.True(await controller.TryBeginAsync(
            ViewerCommand.TemporaryMarkupHand,
            "Space",
            CancellationToken.None));
        Assert.True(controller.CancelForFocusLoss());
        Assert.Equal(MarkupTool.Brush, session.EffectiveTool);
        Assert.False(controller.EndPrimaryKey("Space"));
        Assert.Empty(session.GetRenderSnapshot("A").Operations);
        Assert.Empty(inspection.Commands);
    }

    [Fact]
    public async Task InspectionCommandsRemainRoutedToInspectionOwner()
    {
        var session = new PresentationOverlaySession(PresentationSettings.Default);
        var inspection = new RecordingHoldAction();
        var controller = new ViewerHoldController(new ViewerHoldActionRouter(
            inspection,
            new MarkupTemporaryHandHoldAction(session)));

        Assert.True(await controller.TryBeginAsync(
            ViewerCommand.Peek100,
            "Z",
            CancellationToken.None));
        Assert.True(controller.EndPrimaryKey("Z"));

        Assert.Equal([ViewerCommand.Peek100], inspection.Commands);
        Assert.Equal(1, inspection.EndCount);
    }

    private sealed class RecordingHoldAction : IViewerHoldAction
    {
        public List<ViewerCommand> Commands { get; } = [];

        public int EndCount { get; private set; }

        public Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public void End() => EndCount++;
    }
}
