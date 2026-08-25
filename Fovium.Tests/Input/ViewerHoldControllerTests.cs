using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ViewerHoldControllerTests
{
    [Fact]
    public async Task RepeatedKeyDownBeginsOnceAndMatchingPrimaryKeyUpEndsOnce()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);

        Assert.True(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.False(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.True(controller.EndPrimaryKey("Z"));

        Assert.Equal(1, action.BeginCount);
        Assert.Equal(1, action.EndCount);
        Assert.Null(controller.ActiveCommand);

        Assert.True(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.True(controller.EndPrimaryKey("Z"));
        Assert.Equal(2, action.BeginCount);
        Assert.Equal(2, action.EndCount);
    }

    [Fact]
    public async Task PrimaryKeyReleaseEndsHoldWithoutResolvingChangedModifiers()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);

        await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None);

        Assert.False(controller.EndPrimaryKey("LeftCtrl"));
        Assert.True(controller.EndPrimaryKey("Z"));
        Assert.Equal(1, action.EndCount);
    }

    [Fact]
    public async Task FirstActiveHoldWinsAndSecondHoldIsIgnoredUntilRelease()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);

        await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None);
        var second = await controller.TryBeginAsync(
            ViewerCommand.BlinkCompare,
            "C",
            CancellationToken.None);

        Assert.False(second);
        Assert.Equal(ViewerCommand.Peek100, controller.ActiveCommand);
        Assert.True(controller.EndPrimaryKey("Z"));
        Assert.Equal([ViewerCommand.Peek100], action.Commands);
    }

    [Fact]
    public async Task EscapeOrFocusCancellationMakesSubsequentStaleKeyUpHarmless()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);
        await controller.TryBeginAsync(ViewerCommand.BlinkCompare, "C", CancellationToken.None);

        Assert.True(controller.Cancel());
        Assert.False(controller.EndPrimaryKey("C"));
        Assert.False(controller.Cancel());
        Assert.Equal(1, action.EndCount);
    }

    [Fact]
    public async Task UnrelatedKeyUpDoesNotEndActiveHold()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);
        await controller.TryBeginAsync(ViewerCommand.BlinkCompare, "C", CancellationToken.None);

        Assert.False(controller.EndPrimaryKey("Z"));
        Assert.Equal(ViewerCommand.BlinkCompare, controller.ActiveCommand);
        Assert.Equal(0, action.EndCount);
    }

    [Fact]
    public async Task PressCommandCannotEnterHoldLifecycle()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);

        Assert.False(await controller.TryBeginAsync(ViewerCommand.NextImage, "Right", CancellationToken.None));
        Assert.Empty(action.Commands);
        Assert.Null(controller.ActiveCommand);
    }

    [Fact]
    public async Task PersistentCancellationSuppressesRepeatUntilStalePrimaryKeyUp()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);
        await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None);

        controller.Cancel();
        Assert.False(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.False(controller.EndPrimaryKey("Z"));
        Assert.True(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.Equal(2, action.BeginCount);
    }

    [Fact]
    public async Task FocusLossClearsSuppressionSoFuturePhysicalPressCanBegin()
    {
        var action = new RecordingHoldAction();
        var controller = new ViewerHoldController(action);
        await controller.TryBeginAsync(ViewerCommand.BlinkCompare, "C", CancellationToken.None);
        await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None);

        Assert.True(controller.CancelForFocusLoss());
        Assert.True(await controller.TryBeginAsync(ViewerCommand.Peek100, "Z", CancellationToken.None));
        Assert.Equal(ViewerCommand.Peek100, controller.ActiveCommand);
    }

    private sealed class RecordingHoldAction : IViewerHoldAction
    {
        public List<ViewerCommand> Commands { get; } = [];

        public int BeginCount => Commands.Count;

        public int EndCount { get; private set; }

        public Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public void End() => EndCount++;
    }
}
