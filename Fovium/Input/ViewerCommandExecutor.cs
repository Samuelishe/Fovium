namespace Fovium.Input;

internal interface IViewerCommandTarget
{
    Task PreviousAsync();

    Task NextAsync();

    void ZoomByStepsAtCenter(int steps);

    void Fit();

    void SetPhotographic100AtCenter();

    Task ToggleMatteAsync();

    void ToggleFullscreen();

    Task OpenAsync();

    void ShowSettings();

    void ToggleHighlight();

    void ToggleMarkupTools();

    void TogglePhotoInfo();

    void ToggleHistogram();

    void ToggleColorPicker();

    void UndoMarkup();

    void RedoMarkup();

    void ClearMarkup();

    void AdjustMarkupThickness(double deltaPhysicalPixels);

    void AdjustMarkupOpacity(double delta);

    Task AdjustHighlightRadiusAsync(double deltaPhysicalPixels);

    void SelectHandTool();

    void SelectBrushTool();

    void SelectEraserTool();

    void SelectLineTool();

    void SelectRectangleTool();

    void SelectEllipseTool();

    void SelectArrowTool();
}

internal sealed class ViewerCommandExecutor(IViewerCommandTarget target)
{
    public Task ExecuteAsync(ViewerCommand command)
    {
        if (ViewerCommands.GetDefinition(command).Trigger != ViewerCommandTrigger.Press)
        {
            throw new InvalidOperationException("Hold commands require an explicit begin/end lifecycle.");
        }

        return command switch
        {
            ViewerCommand.PreviousImage => target.PreviousAsync(),
            ViewerCommand.NextImage => target.NextAsync(),
            ViewerCommand.ZoomIn => Execute(() => target.ZoomByStepsAtCenter(1)),
            ViewerCommand.ZoomOut => Execute(() => target.ZoomByStepsAtCenter(-1)),
            ViewerCommand.Fit => Execute(target.Fit),
            ViewerCommand.ActualSize => Execute(target.SetPhotographic100AtCenter),
            ViewerCommand.ToggleMatte => target.ToggleMatteAsync(),
            ViewerCommand.Fullscreen => Execute(target.ToggleFullscreen),
            ViewerCommand.Open => target.OpenAsync(),
            ViewerCommand.Settings => Execute(target.ShowSettings),
            ViewerCommand.ToggleHighlight => Execute(target.ToggleHighlight),
            ViewerCommand.ToggleMarkupTools => Execute(target.ToggleMarkupTools),
            ViewerCommand.TogglePhotoInfo => Execute(target.TogglePhotoInfo),
            ViewerCommand.ToggleHistogram => Execute(target.ToggleHistogram),
            ViewerCommand.ToggleColorPicker => Execute(target.ToggleColorPicker),
            ViewerCommand.MarkupUndo => Execute(target.UndoMarkup),
            ViewerCommand.MarkupRedo => Execute(target.RedoMarkup),
            ViewerCommand.ClearMarkup => Execute(target.ClearMarkup),
            ViewerCommand.DecreaseMarkupThickness => Execute(
                () => target.AdjustMarkupThickness(-1)),
            ViewerCommand.IncreaseMarkupThickness => Execute(
                () => target.AdjustMarkupThickness(1)),
            ViewerCommand.DecreaseMarkupOpacity => Execute(
                () => target.AdjustMarkupOpacity(-0.05)),
            ViewerCommand.IncreaseMarkupOpacity => Execute(
                () => target.AdjustMarkupOpacity(0.05)),
            ViewerCommand.DecreaseHighlightRadius => target.AdjustHighlightRadiusAsync(-4),
            ViewerCommand.IncreaseHighlightRadius => target.AdjustHighlightRadiusAsync(4),
            ViewerCommand.SelectHandTool => Execute(target.SelectHandTool),
            ViewerCommand.SelectBrushTool => Execute(target.SelectBrushTool),
            ViewerCommand.SelectEraserTool => Execute(target.SelectEraserTool),
            ViewerCommand.SelectLineTool => Execute(target.SelectLineTool),
            ViewerCommand.SelectRectangleTool => Execute(target.SelectRectangleTool),
            ViewerCommand.SelectEllipseTool => Execute(target.SelectEllipseTool),
            ViewerCommand.SelectArrowTool => Execute(target.SelectArrowTool),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private static Task Execute(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
