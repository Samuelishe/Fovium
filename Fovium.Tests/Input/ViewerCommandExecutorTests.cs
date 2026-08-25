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
    [InlineData((int)ViewerCommand.DecreaseMarkupThickness, -1, 0)]
    [InlineData((int)ViewerCommand.IncreaseMarkupThickness, 1, 0)]
    [InlineData((int)ViewerCommand.DecreaseMarkupOpacity, 0, -0.05)]
    [InlineData((int)ViewerCommand.IncreaseMarkupOpacity, 0, 0.05)]
    public async Task MarkupStyleCommandsUseExactSteps(
        int commandValue,
        double expectedThickness,
        double expectedOpacity)
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync((ViewerCommand)commandValue);

        Assert.Equal(expectedThickness, target.ThicknessAdjustment);
        Assert.Equal(expectedOpacity, target.OpacityAdjustment, 8);
    }

    [Theory]
    [InlineData((int)ViewerCommand.DecreaseHighlightRadius, -4)]
    [InlineData((int)ViewerCommand.IncreaseHighlightRadius, 4)]
    public async Task HighlightRadiusCommandsUseFourPhysicalPixelSteps(
        int commandValue,
        double expected)
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync((ViewerCommand)commandValue);

        Assert.Equal(expected, target.HighlightRadiusAdjustment);
    }

    [Theory]
    [InlineData((int)ViewerCommand.SelectHandTool, "hand")]
    [InlineData((int)ViewerCommand.SelectBrushTool, "brush")]
    [InlineData((int)ViewerCommand.SelectEraserTool, "eraser")]
    [InlineData((int)ViewerCommand.SelectLineTool, "line")]
    [InlineData((int)ViewerCommand.SelectRectangleTool, "rectangle")]
    [InlineData((int)ViewerCommand.SelectEllipseTool, "ellipse")]
    [InlineData((int)ViewerCommand.SelectArrowTool, "arrow")]
    public async Task ToolSelectionCommandsUseSharedExecutor(int commandValue, string expected)
    {
        var target = new RecordingTarget();

        await new ViewerCommandExecutor(target).ExecuteAsync((ViewerCommand)commandValue);

        Assert.Equal(expected, target.SelectedTool);
    }

    [Theory]
    [InlineData((int)ViewerCommand.Peek100)]
    [InlineData((int)ViewerCommand.BlinkCompare)]
    [InlineData((int)ViewerCommand.TemporaryMarkupHand)]
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

        public double ThicknessAdjustment { get; private set; }

        public double OpacityAdjustment { get; private set; }

        public double HighlightRadiusAdjustment { get; private set; }

        public string? SelectedTool { get; private set; }

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

        public void AdjustMarkupThickness(double deltaPhysicalPixels) =>
            ThicknessAdjustment += deltaPhysicalPixels;

        public void AdjustMarkupOpacity(double delta) => OpacityAdjustment += delta;

        public Task AdjustHighlightRadiusAsync(double deltaPhysicalPixels)
        {
            HighlightRadiusAdjustment += deltaPhysicalPixels;
            return Task.CompletedTask;
        }

        public void SelectHandTool() => SelectedTool = "hand";

        public void SelectBrushTool() => SelectedTool = "brush";

        public void SelectEraserTool() => SelectedTool = "eraser";

        public void SelectLineTool() => SelectedTool = "line";

        public void SelectRectangleTool() => SelectedTool = "rectangle";

        public void SelectEllipseTool() => SelectedTool = "ellipse";

        public void SelectArrowTool() => SelectedTool = "arrow";
    }
}
