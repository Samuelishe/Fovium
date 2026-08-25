using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Fovium.Application;
using Fovium.Imaging;
using Fovium.Input;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Navigation;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Stage;
using Fovium.Viewer;
using ViewerNavigationDirection = Fovium.Navigation.NavigationDirection;

namespace Fovium.Views;

internal sealed partial class ViewerWindow : Window, IViewerCommandTarget
{
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(1.75);

    private readonly ActivationService _activation;
    private readonly ViewerSession<DecodedImage> _session;
    private readonly Localizer _localizer;
    private readonly SettingsService _settings;
    private readonly AmbientStageCoordinator _stageCoordinator;
    private readonly IReadOnlyList<string> _startupPaths;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Cursor _visibleCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private readonly ContextMenu _contextMenu;
    private readonly ViewerCommandExecutor _commandExecutor;
    private readonly ViewerInspectionCoordinator _inspectionCoordinator;
    private readonly ViewerHoldController _holdController;
    private readonly Dictionary<ViewerCommand, MenuItem> _commandMenuItems = [];
    private readonly MenuItem _previousMenuItem;
    private readonly MenuItem _nextMenuItem;
    private readonly IReadOnlyDictionary<StageBackgroundMode, MenuItem> _stageBackgroundMenuItems;
    private readonly MenuItem _matteMenuItem;
    private bool _contextMenuOpen;
    private bool _closed;
    private bool _shutdownCompleted;
    private bool _shutdownStarted;
    private SettingsWindow? _settingsWindow;
    private WindowState _windowStateBeforeFullscreen = WindowState.Maximized;

    public ViewerWindow(
        ActivationService activation,
        ViewerSession<DecodedImage> session,
        Localizer localizer,
        SettingsService settings,
        IReadOnlyList<string> startupPaths)
    {
        _activation = activation;
        _session = session;
        _localizer = localizer;
        _settings = settings;
        _startupPaths = startupPaths;
        _stageCoordinator = new AmbientStageCoordinator(
            new AmbientImageRepository(session),
            new AmbientStagePreparer(),
            settings.Current.Stage);

        InitializeComponent();
        _commandExecutor = new ViewerCommandExecutor(this);
        _inspectionCoordinator = new ViewerInspectionCoordinator(PhotoViewport, session, settings);
        _holdController = new ViewerHoldController(_inspectionCoordinator);
        _previousMenuItem = CreateCommandMenuItem(UiStrings.MenuPrevious, ViewerCommand.PreviousImage);
        _nextMenuItem = CreateCommandMenuItem(UiStrings.MenuNext, ViewerCommand.NextImage);
        _stageBackgroundMenuItems = CreateStageBackgroundMenuItems();
        _matteMenuItem = CreateMatteMenuItem();
        _contextMenu = CreateContextMenu();
        PhotoViewport.ContextMenu = _contextMenu;

        _cursorTimer = new DispatcherTimer { Interval = CursorHideDelay };
        _cursorTimer.Tick += OnCursorTimerTick;
        PhotoViewport.PointerActivity += OnPointerActivity;
        _stageCoordinator.PresentationChanged += OnStagePresentationChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
        KeyDown += OnWindowKeyDown;
        KeyUp += OnWindowKeyUp;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            PhotoViewport.Focus();
            RestartCursorTimer();
            await _settings.InitializeAsync(_lifetimeCancellation.Token);
            ApplyStage(_settings.Current.Stage);
            if (_startupPaths.Count == 0)
            {
                var selected = await PickFilesAsync();
                if (selected.Count == 0)
                {
                    Close();
                    return;
                }

                await OpenPathsAsync(selected);
                return;
            }

            await OpenPathsAsync(_startupPaths);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsRecoverableBoundaryException(exception))
        {
            ShowBoundaryError();
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        _holdController.Cancel();
        _closed = true;
        _cursorTimer.Stop();
        _settingsWindow?.Close();
        _lifetimeCancellation.Cancel();
        PhotoViewport.ClearImage();
        try
        {
            await _settings.FlushAsync();
            await _stageCoordinator.DisposeAsync();
            await _session.DisposeAsync();
        }
        finally
        {
            _shutdownCompleted = true;
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
        _settings.SettingsChanged -= OnSettingsChanged;
        _stageCoordinator.PresentationChanged -= OnStagePresentationChanged;
        _settings.Dispose();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (_holdController.Cancel())
                {
                    return;
                }

                if (WindowState == WindowState.FullScreen)
                {
                    LeaveFullscreen();
                }
                else
                {
                    Close();
                }
            }
            else if (AvaloniaShortcutGestureAdapter.TryCreate(e, out var gesture) &&
                ShortcutResolver.Resolve(_settings.Current.Shortcuts, gesture) is { } command)
            {
                e.Handled = true;
                var definition = ViewerCommands.GetDefinition(command);
                if (definition.Trigger == ViewerCommandTrigger.Hold &&
                    AvaloniaShortcutGestureAdapter.TryGetPrimaryKey(e.Key, out var primaryKey))
                {
                    await _holdController.TryBeginAsync(
                        command,
                        primaryKey,
                        _lifetimeCancellation.Token);
                }
                else
                {
                    await ExecutePersistentCommandAsync(command);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsRecoverableBoundaryException(exception))
        {
            ShowBoundaryError();
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (AvaloniaShortcutGestureAdapter.TryGetPrimaryKey(e.Key, out var primaryKey) &&
            _holdController.EndPrimaryKey(primaryKey))
        {
            e.Handled = true;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e) => _holdController.CancelForFocusLoss();

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                CreateCommandMenuItem(UiStrings.MenuOpen, ViewerCommand.Open),
                _previousMenuItem,
                _nextMenuItem,
                new Separator(),
                CreateCommandMenuItem(UiStrings.MenuFit, ViewerCommand.Fit),
                CreateCommandMenuItem(UiStrings.MenuActualSize, ViewerCommand.ActualSize),
                new Separator(),
                new MenuItem
                {
                    Header = _localizer[UiStrings.MenuStage],
                    ItemsSource = new Control[]
                    {
                        new MenuItem
                        {
                            Header = _localizer[UiStrings.StageBackground],
                            ItemsSource = _stageBackgroundMenuItems.Values,
                        },
                        new Separator(),
                        _matteMenuItem,
                    },
                },
                new Separator(),
                CreateCommandMenuItem(UiStrings.MenuFullscreen, ViewerCommand.Fullscreen),
                new Separator(),
                CreateCommandMenuItem(UiStrings.MenuSettings, ViewerCommand.Settings),
                CreateMenuItem(UiStrings.MenuClose, () =>
                {
                    Close();
                    return Task.CompletedTask;
                }),
            },
        };
        menu.Opening += (_, _) =>
        {
            _holdController.CancelForFocusLoss();
            _contextMenuOpen = true;
            ShowCursor();
            _cursorTimer.Stop();
            _previousMenuItem.IsEnabled = _session.CanNavigate(ViewerNavigationDirection.Previous);
            _nextMenuItem.IsEnabled = _session.CanNavigate(ViewerNavigationDirection.Next);
            foreach (var (mode, item) in _stageBackgroundMenuItems)
            {
                item.IsChecked = _settings.Current.Stage.BackgroundMode == mode;
            }

            _matteMenuItem.IsChecked = _settings.Current.Stage.MatteEnabled;
            UpdateCommandGestures();
        };
        menu.Closing += (_, _) =>
        {
            _contextMenuOpen = false;
            RestartCursorTimer();
        };
        return menu;
    }

    private IReadOnlyDictionary<StageBackgroundMode, MenuItem> CreateStageBackgroundMenuItems()
    {
        var labels = new Dictionary<StageBackgroundMode, string>
        {
            [StageBackgroundMode.Black] = UiStrings.StageBlack,
            [StageBackgroundMode.Neutral] = UiStrings.StageNeutral,
            [StageBackgroundMode.Custom] = UiStrings.StageCustom,
            [StageBackgroundMode.Ambient] = UiStrings.StageAmbient,
        };
        return labels.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var item = new MenuItem
                {
                    Header = _localizer[pair.Value],
                    ToggleType = MenuItemToggleType.Radio,
                };
                item.Click += async (_, _) =>
                {
                    try
                    {
                        await _settings.SetStageAsync(
                            _settings.Current.Stage with { BackgroundMode = pair.Key },
                            _lifetimeCancellation.Token);
                    }
                    catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
                    {
                    }
                };
                return item;
            });
    }

    private MenuItem CreateMatteMenuItem()
    {
        var item = new MenuItem
        {
            Header = _localizer[UiStrings.StageMatte],
            ToggleType = MenuItemToggleType.CheckBox,
        };
        item.Click += async (_, _) =>
        {
            try
            {
                await _settings.ToggleMatteAsync(_lifetimeCancellation.Token);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
        };
        return item;
    }

    private MenuItem CreateCommandMenuItem(string key, ViewerCommand command)
    {
        var item = CreateMenuItem(key, () => ExecutePersistentCommandAsync(command));
        _commandMenuItems[command] = item;
        return item;
    }

    private void UpdateCommandGestures()
    {
        foreach (var (command, item) in _commandMenuItems)
        {
            item.InputGesture = AvaloniaShortcutGestureAdapter.ToAvalonia(
                _settings.Current.Shortcuts.Get(command));
        }
    }

    private MenuItem CreateMenuItem(string key, Func<Task> action)
    {
        var item = new MenuItem { Header = _localizer[key] };
        item.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (IsRecoverableBoundaryException(exception))
            {
                ShowBoundaryError();
            }
        };
        return item;
    }

    private async Task OpenFromPickerAsync()
    {
        if (_closed)
        {
            return;
        }

        var selected = await PickFilesAsync();
        if (selected.Count > 0)
        {
            await OpenPathsAsync(selected);
        }
    }

    private async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        ShowCursor();
        _cursorTimer.Stop();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _localizer[UiStrings.PickerTitle],
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(_localizer[UiStrings.PickerImageType])
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png"],
                        MimeTypes = ["image/jpeg", "image/png"],
                    },
                ],
            });

            return files
                .Select(file => file.TryGetLocalPath())
                .Where(path => path is not null)
                .Cast<string>()
                .ToArray();
        }
        finally
        {
            RestartCursorTimer();
        }
    }

    private async Task OpenPathsAsync(IReadOnlyList<string> paths)
    {
        _holdController.Cancel();
        var plan = ActivationPlan.Create(paths);
        var sequence = await _activation.ResolveAsync(plan, _lifetimeCancellation.Token);
        if (sequence is null)
        {
            return;
        }

        var result = await _session.OpenAsync(sequence, _lifetimeCancellation.Token);
        ApplySelection(result, ImageChangeViewPolicyResolver.ForNewSequence(), showFailure: true);
    }

    private async Task NavigateAsync(ViewerNavigationDirection direction)
    {
        _holdController.Cancel();
        var result = await _session.NavigateAsync(direction, _lifetimeCancellation.Token);
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            _settings.Current.ImageChangeViewPolicy,
            PhotoViewport.CaptureViewTransfer());
        ApplySelection(result, transfer, showFailure: false);
    }

    private void ApplySelection(
        SelectionResult<DecodedImage> result,
        ViewTransfer transfer,
        bool showFailure)
    {
        if (_closed)
        {
            result.Image?.Dispose();
            return;
        }

        if (result.Status == SelectionStatus.Published && result.Image is not null)
        {
            _holdController.Cancel();
            ErrorSurface.IsVisible = false;
            var path = result.Path
                ?? throw new InvalidOperationException("Published selection has no source path.");
            var identity = result.Image.Value.Identity;
            PhotoViewport.SetImage(result.Image, transfer);
            _stageCoordinator.SelectImage(path, identity);
            return;
        }

        result.Image?.Dispose();
        if (!showFailure || result.Status != SelectionStatus.Failed || result.Error is null)
        {
            return;
        }

        PhotoViewport.ClearImage();
        _stageCoordinator.ClearImage();
        ErrorText.Text = LocalizeError(result.Error.Kind);
        ErrorSurface.IsVisible = true;
    }

    private string LocalizeError(ImageLoadErrorKind kind) =>
        _localizer[kind switch
        {
            ImageLoadErrorKind.Missing => UiStrings.ErrorMissing,
            ImageLoadErrorKind.Unsupported => UiStrings.ErrorUnsupported,
            ImageLoadErrorKind.Corrupt => UiStrings.ErrorCorrupt,
            ImageLoadErrorKind.ResourceLimit => UiStrings.ErrorResourceLimit,
            ImageLoadErrorKind.DecodeFailed => UiStrings.ErrorDecodeFailed,
            _ => UiStrings.ErrorDecodeFailed,
        }];

    private void ShowBoundaryError()
    {
        if (_closed)
        {
            return;
        }

        _holdController.Cancel();
        PhotoViewport.ClearImage();
        _stageCoordinator.ClearImage();
        ErrorText.Text = _localizer[UiStrings.ErrorDecodeFailed];
        ErrorSurface.IsVisible = true;
    }

    private void ShowSettings()
    {
        _holdController.Cancel();
        ShowCursor();
        _cursorTimer.Stop();
        if (_settingsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new SettingsWindow(_settings, _localizer);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            RestartCursorTimer();
        };
        window.Show(this);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyStage(e.Settings.Stage);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplyStage(e.Settings.Stage));
        }
    }

    private void ApplyStage(StageSettings stage)
    {
        if (_closed)
        {
            return;
        }

        _holdController.Cancel();
        _stageCoordinator.SetStage(stage);
    }

    private void OnStagePresentationChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyStagePresentation();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyStagePresentation);
        }
    }

    private void ApplyStagePresentation()
    {
        if (_closed)
        {
            return;
        }

        using var presentation = _stageCoordinator.AcquirePresentation();
        PhotoViewport.SetStage(presentation.Stage, presentation.TakeAmbient());
    }

    private static bool IsRecoverableBoundaryException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException;

    private Task ExecutePersistentCommandAsync(ViewerCommand command)
    {
        _holdController.Cancel();
        return _commandExecutor.ExecuteAsync(command);
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            LeaveFullscreen();
            return;
        }

        _windowStateBeforeFullscreen = WindowState;
        WindowState = WindowState.FullScreen;
    }

    private void LeaveFullscreen() => WindowState = _windowStateBeforeFullscreen;

    private void OnPointerActivity(object? sender, EventArgs e)
    {
        ShowCursor();
        RestartCursorTimer();
    }

    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        _cursorTimer.Stop();
        if (!_contextMenuOpen)
        {
            PhotoViewport.Cursor = _hiddenCursor;
        }
    }

    private void ShowCursor() => PhotoViewport.Cursor = _visibleCursor;

    private void RestartCursorTimer()
    {
        if (_lifetimeCancellation.IsCancellationRequested || _contextMenuOpen)
        {
            return;
        }

        _cursorTimer.Stop();
        _cursorTimer.Start();
    }

    Task IViewerCommandTarget.PreviousAsync() => NavigateAsync(ViewerNavigationDirection.Previous);

    Task IViewerCommandTarget.NextAsync() => NavigateAsync(ViewerNavigationDirection.Next);

    void IViewerCommandTarget.ZoomByStepsAtCenter(int steps) =>
        PhotoViewport.ZoomByStepsAtCenter(steps);

    void IViewerCommandTarget.Fit() => PhotoViewport.Fit();

    void IViewerCommandTarget.SetPhotographic100AtCenter() =>
        PhotoViewport.SetPhotographic100AtCenter();

    Task IViewerCommandTarget.ToggleMatteAsync() =>
        _settings.ToggleMatteAsync(_lifetimeCancellation.Token);

    void IViewerCommandTarget.ToggleFullscreen() => ToggleFullscreen();

    Task IViewerCommandTarget.OpenAsync() => OpenFromPickerAsync();

    void IViewerCommandTarget.ShowSettings() => ShowSettings();
}
