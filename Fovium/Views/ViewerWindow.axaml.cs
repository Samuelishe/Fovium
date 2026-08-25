using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Fovium.Application;
using Fovium.Diagnostics;
using Fovium.Imaging;
using Fovium.Input;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Metadata;
using Fovium.Navigation;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Stage;
using Fovium.Viewer;
using ViewerNavigationDirection = Fovium.Navigation.NavigationDirection;

namespace Fovium.Views;

internal sealed partial class ViewerWindow : Window, IViewerCommandTarget
{
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(1.75);
    private static readonly TimeSpan CursorIdlePollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ActivationService _activation;
    private readonly ViewerSession<DecodedImage> _session;
    private readonly Localizer _localizer;
    private readonly SettingsService _settings;
    private readonly AmbientStageCoordinator _stageCoordinator;
    private readonly AmbientSoakTrace _ambientSoakTrace;
    private readonly InteractionRenderDiagnostics _interactionDiagnostics;
    private readonly IReadOnlyList<string> _startupPaths;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Cursor _visibleCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private readonly Cursor _handCursor = new(StandardCursorType.SizeAll);
    private readonly ContextMenu _contextMenu;
    private readonly ViewerCommandExecutor _commandExecutor;
    private readonly ViewerInspectionCoordinator _inspectionCoordinator;
    private readonly ViewerHoldController _holdController;
    private readonly PresentationOverlaySession _presentation;
    private readonly PhotoInfoCoordinator _photoInfo;
    private readonly FloatingOverlayInteraction _markupFloatingOverlay;
    private readonly FloatingOverlayInteraction _photoInfoFloatingOverlay;
    private readonly Dictionary<ViewerCommand, MenuItem> _commandMenuItems = [];
    private readonly MenuItem _previousMenuItem;
    private readonly MenuItem _nextMenuItem;
    private readonly IReadOnlyDictionary<StageBackgroundMode, MenuItem> _stageBackgroundMenuItems;
    private readonly MenuItem _matteMenuItem;
    private readonly MenuItem _photoInfoMenuItem;
    private readonly MenuItem _highlightMenuItem;
    private readonly MenuItem _markupMenuItem;
    private PresentationColor? _appliedMarkupColor;
    private long _lastPointerActivityTimestamp;
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
        _ambientSoakTrace = AmbientSoakTrace.CreateFromEnvironment();
        _interactionDiagnostics = InteractionRenderDiagnostics.CreateFromEnvironment();

        InitializeComponent();
        PhotoViewport.ConfigureInteractionDiagnostics(_interactionDiagnostics);
        MarkupOverlay.ConfigureDiagnostics(_interactionDiagnostics);
        PointerFeedbackOverlay.ConfigureDiagnostics(_interactionDiagnostics);
        if (_ambientSoakTrace.IsEnabled)
        {
            PhotoViewport.EnableAmbientPipelineDiagnostics();
        }

        _presentation = new PresentationOverlaySession(
            settings.Current.Presentation,
            StringComparer.OrdinalIgnoreCase);
        PhotoViewport.ConfigurePresentation(
            _presentation,
            _visibleCursor,
            _hiddenCursor,
            _handCursor,
            MarkupOverlay,
            PointerFeedbackOverlay);
        _photoInfo = new PhotoInfoCoordinator(
            PhotoViewport,
            new MetadataExtractorPhotoMetadataReader());
        _photoInfo.StateChanged += OnPhotoInfoStateChanged;
        _markupFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            MarkupToolsPanel,
            MarkupDragHandle,
            settings.Current.Presentation.MarkupDockPlacement,
            _interactionDiagnostics);
        _photoInfoFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            PhotoInfoPanel,
            PhotoInfoDragHandle,
            settings.Current.Presentation.PhotoInfoPlacement,
            _interactionDiagnostics);
        _markupFloatingOverlay.PlacementCommitted += OnMarkupPlacementCommitted;
        _photoInfoFloatingOverlay.PlacementCommitted += OnPhotoInfoPlacementCommitted;
        _commandExecutor = new ViewerCommandExecutor(this);
        _inspectionCoordinator = new ViewerInspectionCoordinator(PhotoViewport, session, settings);
        _holdController = new ViewerHoldController(new ViewerHoldActionRouter(
            _inspectionCoordinator,
            new MarkupTemporaryHandHoldAction(_presentation)));
        ConfigureMarkupTools();
        ConfigurePhotoInfo();
        _previousMenuItem = CreateCommandMenuItem(
            UiStrings.MenuPrevious,
            ViewerCommand.PreviousImage,
            FoviumIcon.Previous);
        _nextMenuItem = CreateCommandMenuItem(
            UiStrings.MenuNext,
            ViewerCommand.NextImage,
            FoviumIcon.Next);
        _stageBackgroundMenuItems = CreateStageBackgroundMenuItems();
        _matteMenuItem = CreateMatteMenuItem();
        _photoInfoMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandTogglePhotoInfo,
            ViewerCommand.TogglePhotoInfo,
            FoviumIcon.Info);
        _highlightMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandToggleHighlight,
            ViewerCommand.ToggleHighlight,
            FoviumIcon.Highlight);
        _markupMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandToggleMarkupTools,
            ViewerCommand.ToggleMarkupTools,
            FoviumIcon.Markup);
        _contextMenu = CreateContextMenu();
        PhotoViewport.ContextMenu = _contextMenu;

        _cursorTimer = new DispatcherTimer { Interval = CursorIdlePollInterval };
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
            ApplySettings(_settings.Current);
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
        CompleteAmbientSoakTransition();
        _photoInfo.Dispose();
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
#if DEBUG
        var ambientFrames = PhotoViewport.GetAmbientRenderFrameMetrics();
        Console.WriteLine(
            $"Fovium Ambient rendered frames: black fallback " +
            $"{ambientFrames.BlackFallbackRenderedFrameCount}, matching " +
            $"{ambientFrames.MatchingAmbientRenderedFrameCount}, last image " +
            $"{ambientFrames.LastFrame.ImageIdentity}, last Ambient " +
            $"{ambientFrames.LastFrame.AmbientIdentity?.ToString() ?? "none"}.");
#endif
        if (_interactionDiagnostics.IsEnabled)
        {
            var metrics = _interactionDiagnostics.GetMetrics();
            Console.WriteLine(
                $"Fovium interaction: pointer={metrics.PointerMovedCount}, " +
                $"photoRender={metrics.PhotoPresentationRenderCount}, " +
                $"photoSkia={metrics.PhotoSkiaDrawCount}, " +
                $"markup={metrics.MarkupOverlayDrawCount}, " +
                $"pointerDraw={metrics.PointerFeedbackDrawCount}, " +
                $"dockDrag={metrics.FloatingDockDragUpdateCount}, " +
                $"layout={metrics.ViewerLayoutSizeChangeCount}, " +
                $"longestPointerIntervalMs={metrics.LongestPointerEventInterval.TotalMilliseconds:F2}.");
        }

        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
        _handCursor.Dispose();
        _settings.SettingsChanged -= OnSettingsChanged;
        _photoInfo.StateChanged -= OnPhotoInfoStateChanged;
        _stageCoordinator.PresentationChanged -= OnStagePresentationChanged;
        _ambientSoakTrace.Dispose();
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
                ShortcutResolver.Resolve(
                    _settings.Current.Shortcuts,
                    gesture,
                    new ViewerShortcutContext(
                        _presentation.MarkupToolsVisible,
                        _presentation.HighlightEnabled)) is { } command)
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
                CreateCommandMenuItem(UiStrings.MenuOpen, ViewerCommand.Open, FoviumIcon.Open),
                _previousMenuItem,
                _nextMenuItem,
                new Separator(),
                CreateCommandMenuItem(UiStrings.MenuFit, ViewerCommand.Fit, FoviumIcon.Fit),
                CreateCommandMenuItem(
                    UiStrings.MenuActualSize,
                    ViewerCommand.ActualSize,
                    FoviumIcon.ActualSize),
                CreateCommandMenuItem(
                    UiStrings.MenuFullscreen,
                    ViewerCommand.Fullscreen,
                    FoviumIcon.Fullscreen),
                new Separator(),
                new MenuItem
                {
                    Header = _localizer[UiStrings.MenuOverlays],
                    Icon = FoviumIconCatalog.Create(FoviumIcon.Markup),
                    ItemsSource = new Control[]
                    {
                        _photoInfoMenuItem,
                        _highlightMenuItem,
                        _markupMenuItem,
                    },
                },
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
                CreateCommandMenuItem(
                    UiStrings.MenuSettings,
                    ViewerCommand.Settings,
                    FoviumIcon.Settings),
                CreateMenuItem(UiStrings.MenuClose, () =>
                {
                    Close();
                    return Task.CompletedTask;
                }, FoviumIcon.Close),
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
            var overlays = ViewerOverlayMenuState.Capture(
                _presentation,
                _photoInfo.IsVisible,
                _settings.Current.Shortcuts);
            _photoInfoMenuItem.IsChecked = overlays.PhotoInfoChecked;
            _highlightMenuItem.IsChecked = overlays.HighlightChecked;
            _markupMenuItem.IsChecked = overlays.MarkupChecked;
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

    private MenuItem CreateOverlayToggleMenuItem(
        string key,
        ViewerCommand command,
        FoviumIcon icon)
    {
        var item = CreateCommandMenuItem(key, command, icon);
        item.ToggleType = MenuItemToggleType.CheckBox;
        return item;
    }

    private MenuItem CreateCommandMenuItem(
        string key,
        ViewerCommand command,
        FoviumIcon? icon = null)
    {
        var item = CreateMenuItem(key, () => ExecutePersistentCommandAsync(command), icon);
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

    private MenuItem CreateMenuItem(
        string key,
        Func<Task> action,
        FoviumIcon? icon = null)
    {
        var item = new MenuItem
        {
            Header = _localizer[key],
            Icon = icon is { } value ? FoviumIconCatalog.Create(value) : null,
        };
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
        CompleteAmbientSoakTransition();
        var plan = ActivationPlan.Create(paths);
        var sequence = await _activation.ResolveAsync(plan, _lifetimeCancellation.Token);
        if (sequence is null)
        {
            return;
        }

        var result = await _session.OpenAsync(sequence, _lifetimeCancellation.Token);
        _presentation.StartNewSequence();
        _photoInfo.BeginNewSequence();
        ApplySelection(result, ImageChangeViewPolicyResolver.ForNewSequence(), showFailure: true);
    }

    private async Task NavigateAsync(ViewerNavigationDirection direction)
    {
        _holdController.Cancel();
        CompleteAmbientSoakTransition();
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
            var initialFrames = PhotoViewport.GetAmbientRenderFrameMetrics();
            using var presentation = _stageCoordinator.BeginImageSelection(path, identity);
            var initialMatchingAmbient = presentation.Ambient is not null;
            PhotoViewport.SetPresentation(result.Image, transfer, path, presentation);
            _stageCoordinator.StartCurrentImageWork();
            _ambientSoakTrace.BeginTransition(
                result,
                result.Image.Value,
                initialMatchingAmbient,
                initialFrames);
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
            ApplySettings(e.Settings);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplySettings(e.Settings));
        }
    }

    private void ApplySettings(FoviumSettings settings)
    {
        ApplyStage(settings.Stage);
        _presentation.ApplySettings(settings.Presentation);
        ApplyMarkupToolsUi();
        UpdateMarkupToolTips();
        _markupFloatingOverlay.SetPlacement(settings.Presentation.MarkupDockPlacement);
        _photoInfoFloatingOverlay.SetPlacement(settings.Presentation.PhotoInfoPlacement);
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
        PhotoViewport.SetStage(presentation);
    }

    private void CompleteAmbientSoakTransition()
    {
        if (!_ambientSoakTrace.IsEnabled)
        {
            return;
        }

        _ambientSoakTrace.CompleteCurrent(
            _session.GetMetrics(),
            _stageCoordinator.GetMetrics(),
            PhotoViewport.GetAmbientRenderFrameMetrics(),
            PhotoViewport.CaptureAmbientPresentationState());
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
        _lastPointerActivityTimestamp = Stopwatch.GetTimestamp();
        ShowCursor();
        EnsureCursorTimerRunning();
    }

    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        if (!_contextMenuOpen &&
            _lastPointerActivityTimestamp != 0 &&
            Stopwatch.GetElapsedTime(_lastPointerActivityTimestamp) >= CursorHideDelay)
        {
            PhotoViewport.SetViewerCursor(_hiddenCursor);
        }
    }

    private void ShowCursor() => PhotoViewport.SetViewerCursor(_visibleCursor);

    private void RestartCursorTimer()
    {
        if (_lifetimeCancellation.IsCancellationRequested || _contextMenuOpen)
        {
            return;
        }

        _lastPointerActivityTimestamp = Stopwatch.GetTimestamp();
        EnsureCursorTimerRunning();
    }

    private void EnsureCursorTimerRunning()
    {
        if (!_cursorTimer.IsEnabled)
        {
            _cursorTimer.Start();
        }
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

    void IViewerCommandTarget.ToggleHighlight() => _presentation.ToggleHighlight();

    void IViewerCommandTarget.ToggleMarkupTools()
    {
        _presentation.ToggleMarkupTools();
        _markupFloatingOverlay.ApplyPlacement();
    }

    void IViewerCommandTarget.TogglePhotoInfo()
    {
        _photoInfo.Toggle();
        _photoInfoFloatingOverlay.ApplyPlacement();
    }

    void IViewerCommandTarget.UndoMarkup() => _presentation.UndoCurrent();

    void IViewerCommandTarget.RedoMarkup() => _presentation.RedoCurrent();

    void IViewerCommandTarget.ClearMarkup() => _presentation.ClearCurrentFromCommand();

    void IViewerCommandTarget.AdjustMarkupThickness(double deltaPhysicalPixels) =>
        _presentation.AdjustActiveStrokePhysicalPixels(deltaPhysicalPixels);

    void IViewerCommandTarget.AdjustMarkupOpacity(double delta) =>
        _presentation.AdjustActiveOpacity(delta);

    Task IViewerCommandTarget.AdjustHighlightRadiusAsync(double deltaPhysicalPixels)
    {
        var current = _settings.Current.Presentation;
        return _settings.SetPresentationAsync(
            current.AdjustHighlightRadius(deltaPhysicalPixels),
            _lifetimeCancellation.Token);
    }

    void IViewerCommandTarget.SelectHandTool() => SelectMarkupTool(MarkupTool.Hand);

    void IViewerCommandTarget.SelectBrushTool() => SelectMarkupTool(MarkupTool.Brush);

    void IViewerCommandTarget.SelectEraserTool() => SelectMarkupTool(MarkupTool.Eraser);

    void IViewerCommandTarget.SelectLineTool() => SelectMarkupTool(MarkupTool.Line);

    void IViewerCommandTarget.SelectRectangleTool() => SelectMarkupTool(MarkupTool.Rectangle);

    void IViewerCommandTarget.SelectEllipseTool() => SelectMarkupTool(MarkupTool.Ellipse);

    void IViewerCommandTarget.SelectArrowTool() => SelectMarkupTool(MarkupTool.Arrow);

    private void ConfigureMarkupTools()
    {
        MarkupHandButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Hand);
        MarkupBrushButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Brush);
        MarkupEraserButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Eraser);
        MarkupLineButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Line);
        MarkupRectangleButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Rectangle);
        MarkupEllipseButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Ellipse);
        MarkupArrowButton.Click += (_, _) => SelectMarkupTool(MarkupTool.Arrow);
        MarkupColorButton.Click += async (_, _) => await EditMarkupColorAsync();
        MarkupStrokeSlider.ValueChanged += (_, _) =>
        {
            _presentation.SetActiveStrokePhysicalPixels(MarkupStrokeSlider.Value);
            MarkupStrokeValue.Text = Math.Round(MarkupStrokeSlider.Value)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        };
        MarkupOpacitySlider.ValueChanged += (_, _) =>
        {
            _presentation.SetActiveOpacity(MarkupOpacitySlider.Value / 100);
            MarkupOpacityValue.Text = $"{_presentation.ActiveOpacity:P0}";
        };
        MarkupUndoButton.Click += (_, _) => _presentation.UndoCurrent();
        MarkupRedoButton.Click += (_, _) => _presentation.RedoCurrent();
        MarkupClearButton.Click += (_, _) => _presentation.ClearCurrent();
        _presentation.Changed += OnPresentationChanged;
        MarkupToolsPanel.AddHandler(
            PointerPressedEvent,
            OnMarkupPanelPointerPressed,
            RoutingStrategies.Tunnel);
        ViewerRoot.SizeChanged += (_, _) =>
        {
            _interactionDiagnostics.RecordViewerLayoutSizeChange();
            _markupFloatingOverlay.ApplyPlacement();
            _photoInfoFloatingOverlay.ApplyPlacement();
        };

        MarkupHandButton.Content = FoviumIconCatalog.Create(FoviumIcon.Hand);
        MarkupBrushButton.Content = FoviumIconCatalog.Create(FoviumIcon.Brush);
        MarkupEraserButton.Content = FoviumIconCatalog.Create(FoviumIcon.Eraser);
        MarkupLineButton.Content = FoviumIconCatalog.Create(FoviumIcon.Line);
        MarkupRectangleButton.Content = FoviumIconCatalog.Create(FoviumIcon.Rectangle);
        MarkupEllipseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Ellipse);
        MarkupArrowButton.Content = FoviumIconCatalog.Create(FoviumIcon.Arrow);
        MarkupUndoButton.Content = FoviumIconCatalog.Create(FoviumIcon.Undo);
        MarkupRedoButton.Content = FoviumIconCatalog.Create(FoviumIcon.Redo);
        MarkupClearButton.Content = FoviumIconCatalog.Create(FoviumIcon.Clear);
        MarkupStrokeText.Text = _localizer[UiStrings.PresentationStroke];
        MarkupOpacityText.Text = _localizer[UiStrings.PresentationOpacity];
        ToolTip.SetTip(MarkupDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(MarkupColorButton, _localizer[UiStrings.PresentationColor]);
        UpdateMarkupToolTips();
        ApplyMarkupToolsUi();
    }

    private void SelectMarkupTool(MarkupTool tool)
    {
        _holdController.Cancel();
        _presentation.SetActiveTool(tool);
        PhotoViewport.Focus();
    }

    private void OnPresentationChanged(object? sender, PresentationChangedEventArgs e)
    {
        if (InteractionRenderRouting.ForPresentationChange(e.Kind)
            .HasFlag(InteractionRenderLayer.Toolbar))
        {
            ApplyMarkupToolsUi();
        }
    }

    private async Task EditMarkupColorAsync()
    {
        var original = _presentation.ActiveColor;
        var editor = new ColorEditorWindow(
            new StageColor(original.Red, original.Green, original.Blue),
            _localizer,
            _localizer[UiStrings.PresentationMarkupColor]);
        editor.ColorChanged += (_, args) => _presentation.SetActiveColor(
            new PresentationColor(args.Color.Red, args.Color.Green, args.Color.Blue));
        var accepted = await editor.ShowDialog<bool>(this);
        if (!accepted)
        {
            _presentation.SetActiveColor(original);
        }
    }

    private void ApplyMarkupToolsUi()
    {
        if (!IsInitialized)
        {
            return;
        }

        MarkupToolsPanel.IsVisible = _presentation.MarkupToolsVisible;
        var effectiveTool = _presentation.EffectiveTool;
        MarkupHandButton.Classes.Set("accent", effectiveTool == MarkupTool.Hand);
        MarkupBrushButton.Classes.Set("accent", effectiveTool == MarkupTool.Brush);
        MarkupEraserButton.Classes.Set("accent", effectiveTool == MarkupTool.Eraser);
        MarkupLineButton.Classes.Set("accent", effectiveTool == MarkupTool.Line);
        MarkupRectangleButton.Classes.Set("accent", effectiveTool == MarkupTool.Rectangle);
        MarkupEllipseButton.Classes.Set("accent", effectiveTool == MarkupTool.Ellipse);
        MarkupArrowButton.Classes.Set("accent", effectiveTool == MarkupTool.Arrow);
        MarkupUndoButton.IsEnabled = _presentation.CanUndo;
        MarkupRedoButton.IsEnabled = _presentation.CanRedo;
        MarkupClearButton.IsEnabled = _presentation.CanClear;
        var color = _presentation.ActiveColor;
        if (_appliedMarkupColor != color)
        {
            _appliedMarkupColor = color;
            MarkupColorSwatch.Background = new SolidColorBrush(
                Color.FromRgb(color.Red, color.Green, color.Blue));
        }
        if (Math.Abs(MarkupStrokeSlider.Value - _presentation.ActiveStrokePhysicalPixels) > 0.001)
        {
            MarkupStrokeSlider.Value = _presentation.ActiveStrokePhysicalPixels;
        }

        MarkupStrokeValue.Text = Math.Round(_presentation.ActiveStrokePhysicalPixels)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (Math.Abs(MarkupOpacitySlider.Value - _presentation.ActiveOpacity * 100) > 0.001)
        {
            MarkupOpacitySlider.Value = _presentation.ActiveOpacity * 100;
        }

        MarkupOpacityValue.Text = $"{_presentation.ActiveOpacity:P0}";
    }

    private void UpdateMarkupToolTips()
    {
        SetCommandToolTip(MarkupHandButton, ViewerCommand.SelectHandTool);
        SetCommandToolTip(MarkupBrushButton, ViewerCommand.SelectBrushTool);
        SetCommandToolTip(MarkupEraserButton, ViewerCommand.SelectEraserTool);
        SetCommandToolTip(MarkupLineButton, ViewerCommand.SelectLineTool);
        SetCommandToolTip(MarkupRectangleButton, ViewerCommand.SelectRectangleTool);
        SetCommandToolTip(MarkupEllipseButton, ViewerCommand.SelectEllipseTool);
        SetCommandToolTip(MarkupArrowButton, ViewerCommand.SelectArrowTool);
        SetCommandToolTip(MarkupUndoButton, ViewerCommand.MarkupUndo);
        SetCommandToolTip(MarkupRedoButton, ViewerCommand.MarkupRedo);
        SetCommandToolTip(MarkupClearButton, ViewerCommand.ClearMarkup);
    }

    private void SetCommandToolTip(Control control, ViewerCommand command)
    {
        var name = _localizer[UiStrings.ForCommand(command)];
        ToolTip.SetTip(
            control,
            ViewerCommandDisplay.FormatToolTip(name, _settings.Current.Shortcuts.Get(command)));
    }

    private void OnMarkupPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _holdController.Cancel();
        ShowCursor();
    }

    private void ConfigurePhotoInfo()
    {
        PhotoInfoTitleText.Text = _localizer[UiStrings.PhotoInfoTitle];
        PhotoInfoCloseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        ToolTip.SetTip(PhotoInfoDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(PhotoInfoCloseButton, _localizer[UiStrings.PhotoInfoClose]);
        PhotoInfoCloseButton.Click += (_, _) =>
        {
            _photoInfo.SetVisible(false);
            PhotoViewport.Focus();
        };
        PhotoInfoPanel.AddHandler(
            PointerPressedEvent,
            OnMarkupPanelPointerPressed,
            RoutingStrategies.Tunnel);
        ApplyPhotoInfoUi();
    }

    private void OnPhotoInfoStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPhotoInfoUi();
            return;
        }

        Dispatcher.UIThread.Post(ApplyPhotoInfoUi);
    }

    private void ApplyPhotoInfoUi()
    {
        if (!IsInitialized || _closed)
        {
            return;
        }

        PhotoInfoPanel.IsVisible = _photoInfo.IsVisible;
        var state = _photoInfo.CurrentState;
        if (!_photoInfo.IsVisible || state is null)
        {
            SetPhotoInfoLine(PhotoInfoCameraText, null);
            SetPhotoInfoLine(PhotoInfoLensText, null);
            SetPhotoInfoLine(PhotoInfoExposureText, null);
            SetPhotoInfoLine(PhotoInfoDimensionsText, null);
            SetPhotoInfoLine(PhotoInfoDateText, null);
            SetPhotoInfoLine(PhotoInfoFileText, null);
            return;
        }

        var culture = System.Globalization.CultureInfo.GetCultureInfo(
            _localizer.Locale == "ru" ? "ru-RU" : "en-US");
        var text = PhotoInfoFormatter.Format(state, culture);
        SetPhotoInfoLine(PhotoInfoCameraText, text.Camera);
        SetPhotoInfoLine(PhotoInfoLensText, text.Lens);
        SetPhotoInfoLine(PhotoInfoExposureText, text.Exposure);
        SetPhotoInfoLine(PhotoInfoDimensionsText, text.Dimensions);
        SetPhotoInfoLine(PhotoInfoDateText, text.CaptureDateTime);
        SetPhotoInfoLine(PhotoInfoFileText, text.File);
        _photoInfoFloatingOverlay.ApplyPlacement();
    }

    private static void SetPhotoInfoLine(TextBlock textBlock, string? value)
    {
        textBlock.Text = value;
        textBlock.IsVisible = !string.IsNullOrEmpty(value);
    }

    private async void OnMarkupPlacementCommitted(FloatingOverlayPlacement placement)
    {
        await PersistOverlayPlacementAsync(
            _settings.Current.Presentation with { MarkupDockPlacement = placement });
    }

    private async void OnPhotoInfoPlacementCommitted(FloatingOverlayPlacement placement)
    {
        await PersistOverlayPlacementAsync(
            _settings.Current.Presentation with { PhotoInfoPlacement = placement });
    }

    private async Task PersistOverlayPlacementAsync(PresentationSettings presentation)
    {
        try
        {
            await _settings.SetPresentationAsync(presentation, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        PhotoViewport.Focus();
    }

}
