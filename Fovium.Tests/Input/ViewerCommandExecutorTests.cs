using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ViewerCommandExecutorTests
{
    [Fact]
    public async Task ZoomInCommandUsesOneViewerStep()
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync(ViewerCommand.ZoomIn);

        Assert.Equal(1, target.ZoomSteps);
    }

    [Fact]
    public async Task ZoomOutCommandUsesInverseViewerStep()
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync(ViewerCommand.ZoomOut);

        Assert.Equal(-1, target.ZoomSteps);
    }

    [Fact]
    public async Task FitCommandAlwaysFits()
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync(ViewerCommand.Fit);

        Assert.Equal(1, target.FitCount);
        Assert.Equal(0, target.ActualSizeCount);
    }

    [Fact]
    public async Task ActualSizeCommandSetsPhotographicScaleOnePath()
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync(ViewerCommand.ActualSize);

        Assert.Equal(1, target.ActualSizeCount);
        Assert.Equal(0, target.FitCount);
    }

    [Theory]
    [InlineData((int)ViewerCommand.ToggleHighlight)]
    [InlineData((int)ViewerCommand.ToggleMarkupTools)]
    public async Task PresentationToggleCommandsUseSharedExecutor(int commandValue)
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync((ViewerCommand)commandValue);

        Assert.Equal(1, target.PresentationToggleCount);
    }

    [Theory]
    [InlineData((int)ViewerCommand.MarkupUndo, "undo")]
    [InlineData((int)ViewerCommand.MarkupRedo, "redo")]
    [InlineData((int)ViewerCommand.ClearMarkup, "clear")]
    public async Task MarkupHistoryCommandsUseSharedExecutor(int commandValue, string expectedAction)
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync((ViewerCommand)commandValue);

        Assert.Equal([expectedAction], target.MarkupActions);
    }

    [Theory]
    [InlineData((int)ViewerCommand.Peek100)]
    [InlineData((int)ViewerCommand.BlinkCompare)]
    public async Task HoldCommandsCannotRunThroughOneShotExecutor(int commandValue)
    {
        var executor = new ViewerCommandExecutor(new RecordingTarget());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync((ViewerCommand)commandValue));
    }

    private sealed class RecordingTarget : IViewerCommandTarget
    {
        public int ZoomSteps { get; private set; }

        public int FitCount { get; private set; }

        public int ActualSizeCount { get; private set; }

        public int PresentationToggleCount { get; private set; }

        public List<string> MarkupActions { get; } = [];

        public Task PreviousAsync() => Task.CompletedTask;

        public Task NextAsync() => Task.CompletedTask;

        public void ZoomByStepsAtCenter(int steps) => ZoomSteps += steps;

        public void Fit() => FitCount++;

        public void SetPhotographic100AtCenter() => ActualSizeCount++;

        public Task ToggleMatteAsync() => Task.CompletedTask;

        public void ToggleFullscreen()
        {
        }

        public Task OpenAsync() => Task.CompletedTask;

        public void ShowSettings()
        {
        }

        public void ToggleHighlight() => PresentationToggleCount++;

        public void ToggleMarkupTools() => PresentationToggleCount++;

        public void UndoMarkup() => MarkupActions.Add("undo");

        public void RedoMarkup() => MarkupActions.Add("redo");

        public void ClearMarkup() => MarkupActions.Add("clear");
    }
}
