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
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private static Task Execute(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
