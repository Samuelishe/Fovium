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
using Fovium.ColorPicking;
using Fovium.ColorManagement;
using Fovium.Diagnostics;
using Fovium.Histogram;
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
    private readonly DispatcherTimer _displayProfileRefreshTimer;
    private readonly IDisplayColorProfileProvider _displayProfileProvider;
    private readonly SemaphoreSlim _displayProfileRefreshGate = new(1, 1);
    private readonly Cursor _visibleCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private readonly Cursor _handCursor = new(StandardCursorType.SizeAll);
    private readonly ContextMenu _contextMenu;
    private readonly ViewerCommandExecutor _commandExecutor;
    private readonly ViewerInspectionCoordinator _inspectionCoordinator;
    private readonly ViewerHoldController _holdController;
    private readonly PresentationOverlaySession _presentation;
    private readonly PhotoInfoCoordinator _photoInfo;
    private readonly HistogramCoordinator _histogram;
    private readonly ColorPickerSession _colorPicker;
    private readonly PhotoColorSampler _photoColorSampler;
    private readonly FloatingOverlayInteraction _markupFloatingOverlay;
    private readonly FloatingOverlayInteraction _photoInfoFloatingOverlay;
    private readonly FloatingOverlayInteraction _histogramFloatingOverlay;
    private readonly FloatingOverlayInteraction _colorPickerFloatingOverlay;
    private readonly Dictionary<ViewerCommand, MenuItem> _commandMenuItems = [];
    private readonly MenuItem _previousMenuItem;
    private readonly MenuItem _nextMenuItem;
    private readonly IReadOnlyDictionary<StageBackgroundMode, MenuItem> _stageBackgroundMenuItems;
    private readonly MenuItem _matteMenuItem;
    private readonly MenuItem _photoPresentationMenuItem;
    private readonly MenuItem _photoInfoMenuItem;
    private readonly MenuItem _histogramMenuItem;
    private readonly MenuItem _colorPickerMenuItem;
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
    private nint _currentColorMonitorHandle;
    private int _displayProfileRefreshGeneration;
    private bool _forceDisplayProfileRefresh;
    private bool _appliedMonitorColorManagementEnabled;

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
        var lcmsAvailability = new LittleCmsRuntimeLocator().TryLoad();
        PhotoViewport.ConfigureMonitorColorManagement(
            new LittleCmsColorTransformEngine(lcmsAvailability),
            settings.Current.MonitorColorManagementEnabled);
        _appliedMonitorColorManagementEnabled = settings.Current.MonitorColorManagementEnabled;
        _displayProfileProvider = OperatingSystem.IsWindows()
            ? new WindowsDisplayColorProfileProvider()
            : new UnsupportedDisplayColorProfileProvider();
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
        _histogram = new HistogramCoordinator(
            PhotoViewport,
            new SkiaDecodedHistogramReader());
        _histogram.StateChanged += OnHistogramStateChanged;
        _colorPicker = new ColorPickerSession();
        _photoColorSampler = new PhotoColorSampler();
        _colorPicker.Changed += OnColorPickerChanged;
        PhotoViewport.ColorSampleRequested += OnColorSampleRequested;
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
        _histogramFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            HistogramPanel,
            HistogramDragHandle,
            settings.Current.Presentation.HistogramPlacement,
            _interactionDiagnostics);
        _colorPickerFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            ColorPickerPanel,
            ColorPickerDragHandle,
            settings.Current.Presentation.ColorPickerPlacement,
            _interactionDiagnostics);
        _markupFloatingOverlay.PlacementCommitted += OnMarkupPlacementCommitted;
        _photoInfoFloatingOverlay.PlacementCommitted += OnPhotoInfoPlacementCommitted;
        _histogramFloatingOverlay.PlacementCommitted += OnHistogramPlacementCommitted;
        _colorPickerFloatingOverlay.PlacementCommitted += OnColorPickerPlacementCommitted;
        _commandExecutor = new ViewerCommandExecutor(this);
        _inspectionCoordinator = new ViewerInspectionCoordinator(PhotoViewport, session, settings);
        _holdController = new ViewerHoldController(new ViewerHoldActionRouter(
            _inspectionCoordinator,
            new MarkupTemporaryHandHoldAction(_presentation, () => _colorPicker.IsVisible)));
        ConfigureMarkupTools();
        ConfigurePhotoInfo();
        ConfigureHistogram();
        ConfigureColorPicker();
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
        _photoPresentationMenuItem = CreateCommandMenuItem(
            UiStrings.CommandTogglePhotoPresentation,
            ViewerCommand.TogglePhotoPresentation);
        _photoPresentationMenuItem.ToggleType = MenuItemToggleType.CheckBox;
        _photoInfoMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandTogglePhotoInfo,
            ViewerCommand.TogglePhotoInfo,
            FoviumIcon.Info);
        _histogramMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandToggleHistogram,
            ViewerCommand.ToggleHistogram,
            FoviumIcon.Histogram);
        _colorPickerMenuItem = CreateOverlayToggleMenuItem(
            UiStrings.CommandToggleColorPicker,
            ViewerCommand.ToggleColorPicker,
            FoviumIcon.ColorPicker);
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
        _displayProfileRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _displayProfileRefreshTimer.Tick += OnDisplayProfileRefreshTimerTick;
        PhotoViewport.PointerActivity += OnPointerActivity;
        _stageCoordinator.PresentationChanged += OnStagePresentationChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
        Activated += OnDisplayRefreshRequired;
        PositionChanged += OnDisplayRefreshTrigger;
        SizeChanged += OnDisplayRefreshTrigger;
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
            Screens.Changed += OnDisplayRefreshRequired;
            ScheduleDisplayProfileRefresh(forceProfileRefresh: true);
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
        _histogram.Dispose();
        _colorPicker.SetVisible(false);
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

        if (string.Equals(
            Environment.GetEnvironmentVariable("FOVIUM_HISTOGRAM_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal))
        {
            var metrics = _histogram.Metrics;
            Console.WriteLine(
                $"Fovium histogram: started={metrics.ComputationsStarted}, " +
                $"completed={metrics.ComputationsCompleted}, cacheHits={metrics.CacheHits}, " +
                $"canceled={metrics.Canceled}, stale={metrics.StaleResults}, " +
                $"failures={metrics.Failures}, lastMs={metrics.LastComputeDuration.TotalMilliseconds:F2}, " +
                $"lastSamples={metrics.LastSampleCount}, sampled={metrics.LastWasSampled}.");
        }

        if (string.Equals(
            Environment.GetEnvironmentVariable("FOVIUM_COLOR_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal))
        {
            var metrics = PhotoViewport.MonitorColorMetrics;
            Console.WriteLine(
                $"Fovium color: state={PhotoViewport.MonitorColorState}, " +
                $"requests={metrics?.Requests ?? 0}, coalesced={metrics?.CoalescedRequests ?? 0}, " +
                $"completed={metrics?.Completed ?? 0}, stale={metrics?.StaleResults ?? 0}, " +
                $"failures={metrics?.Failures ?? 0}, rasterBytes={metrics?.CurrentRasterBytes ?? 0}, " +
                $"maxRasterBytes={metrics?.MaximumRasterBytes ?? 0}.");
            Console.WriteLine(
                $"Fovium color timing: raster={metrics?.LastRasterSize.Width ?? 0}x" +
                $"{metrics?.LastRasterSize.Height ?? 0}, maxRasterBytes={metrics?.MaximumRasterBytes ?? 0}, " +
                $"sourceReadMs={metrics?.LastSourceReadDuration.TotalMilliseconds ?? 0:F2}, " +
                $"lcmsMs={metrics?.LastTransformDuration.TotalMilliseconds ?? 0:F2}, " +
                $"finalizeMs={metrics?.LastFinalizationDuration.TotalMilliseconds ?? 0:F2}, " +
                $"requestToWorkerMs={metrics?.LastRequestToWorkerStartDuration.TotalMilliseconds ?? 0:F2}.");
            Console.WriteLine(
                $"Fovium color interaction: geometryRequests={metrics?.GeometryRequests ?? 0}, " +
                $"managedSourceFrames={metrics?.ManagedSourceFrames ?? 0}, " +
                $"sourceChanges={metrics?.SourceChanges ?? 0}, destinationChanges={metrics?.DestinationChanges ?? 0}, " +
                $"active={metrics?.Active ?? 0}, pending={metrics?.Pending ?? 0}, " +
                $"matteWithoutPhotoFrames={metrics?.MatteWithoutPhotoFrames ?? 0}, " +
                $"atomicCommits={metrics?.AtomicPresentationCommits ?? 0}, " +
                $"lastAtomicWaitMs={metrics?.LastAtomicPresentationWait.TotalMilliseconds ?? 0:F2}, " +
                $"maxAtomicWaitMs={metrics?.MaximumAtomicPresentationWait.TotalMilliseconds ?? 0:F2}.");
        }

        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
        _handCursor.Dispose();
        _settings.SettingsChanged -= OnSettingsChanged;
        Screens.Changed -= OnDisplayRefreshRequired;
        Activated -= OnDisplayRefreshRequired;
        PositionChanged -= OnDisplayRefreshTrigger;
        SizeChanged -= OnDisplayRefreshTrigger;
        _displayProfileRefreshTimer.Stop();
        _displayProfileRefreshTimer.Tick -= OnDisplayProfileRefreshTimerTick;
        PhotoViewport.ShutdownMonitorColorManagement();
        _photoInfo.StateChanged -= OnPhotoInfoStateChanged;
        _histogram.StateChanged -= OnHistogramStateChanged;
        _colorPicker.Changed -= OnColorPickerChanged;
        PhotoViewport.ColorSampleRequested -= OnColorSampleRequested;
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
                        _presentation.HighlightEnabled,
                        _colorPicker.IsVisible)) is { } command)
            {
                e.Handled = true;
                if (!PhotoPresentationInputPolicy.Allows(
                        command,
                        PhotoViewport.PhotoPresentationViewEnabled))
                {
                    return;
                }

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
                _photoPresentationMenuItem,
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
                        _histogramMenuItem,
                        _colorPickerMenuItem,
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
            _photoPresentationMenuItem.IsChecked = PhotoViewport.PhotoPresentationViewEnabled;
            _commandMenuItems[ViewerCommand.Fit].IsEnabled =
                !PhotoViewport.PhotoPresentationViewEnabled;
            _commandMenuItems[ViewerCommand.ActualSize].IsEnabled =
                !PhotoViewport.PhotoPresentationViewEnabled;
            var overlays = ViewerOverlayMenuState.Capture(
                _presentation,
                _photoInfo.IsVisible,
                _histogram.IsVisible,
                _colorPicker.IsVisible,
                _settings.Current.Shortcuts);
            _photoInfoMenuItem.IsChecked = overlays.PhotoInfoChecked;
            _histogramMenuItem.IsChecked = overlays.HistogramChecked;
            _colorPickerMenuItem.IsChecked = overlays.ColorPickerChecked;
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
                        Patterns = [.. ImageFormatCapabilities.FilePickerPatterns],
                        MimeTypes = [.. ImageFormatCapabilities.FilePickerMimeTypes],
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
        _histogram.BeginNewSequence();
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
        PhotoViewport.SetPhotoPresentationViewSettings(settings.PhotoPresentationView);
        _presentation.ApplySettings(settings.Presentation);
        ApplyMarkupToolsUi();
        UpdateMarkupToolTips();
        _markupFloatingOverlay.SetPlacement(settings.Presentation.MarkupDockPlacement);
        _photoInfoFloatingOverlay.SetPlacement(settings.Presentation.PhotoInfoPlacement);
        _histogramFloatingOverlay.SetPlacement(settings.Presentation.HistogramPlacement);
        _colorPickerFloatingOverlay.SetPlacement(settings.Presentation.ColorPickerPlacement);
        if (_appliedMonitorColorManagementEnabled != settings.MonitorColorManagementEnabled)
        {
            _appliedMonitorColorManagementEnabled = settings.MonitorColorManagementEnabled;
            PhotoViewport.SetMonitorColorManagementEnabled(settings.MonitorColorManagementEnabled);
            ScheduleDisplayProfileRefresh(forceProfileRefresh: true);
        }
    }

    private void OnDisplayRefreshTrigger(object? sender, EventArgs e) =>
        ScheduleDisplayProfileRefresh();

    private void OnDisplayRefreshRequired(object? sender, EventArgs e) =>
        ScheduleDisplayProfileRefresh(forceProfileRefresh: true);

    private void ScheduleDisplayProfileRefresh(bool forceProfileRefresh = false)
    {
        if (_closed)
        {
            return;
        }

        _forceDisplayProfileRefresh |= forceProfileRefresh;
        _displayProfileRefreshTimer.Stop();
        _displayProfileRefreshTimer.Start();
    }

    private void OnDisplayProfileRefreshTimerTick(object? sender, EventArgs e)
    {
        _displayProfileRefreshTimer.Stop();
        var forceProfileRefresh = _forceDisplayProfileRefresh;
        _forceDisplayProfileRefresh = false;
        _ = RefreshDisplayProfileAsync(forceProfileRefresh);
    }

    private async Task RefreshDisplayProfileAsync(bool forceProfileRefresh)
    {
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        var generation = Interlocked.Increment(ref _displayProfileRefreshGeneration);
        var currentMonitor = _currentColorMonitorHandle;
        var gateAcquired = false;
        DisplayProfileResolution resolution;
        try
        {
            await _displayProfileRefreshGate.WaitAsync(_lifetimeCancellation.Token);
            gateAcquired = true;
            if (generation != Volatile.Read(ref _displayProfileRefreshGeneration))
            {
                return;
            }

            resolution = await Task.Run(
                () => _displayProfileProvider.ResolveForWindow(
                    handle,
                    currentMonitor,
                    forceProfileRefresh),
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (IsRecoverableBoundaryException(exception))
        {
            resolution = new DisplayProfileResolution(
                MonitorColorState.DestinationUnavailable,
                null,
                $"Display profile refresh failed ({exception.GetType().Name}).");
        }
        finally
        {
            if (gateAcquired)
            {
                _displayProfileRefreshGate.Release();
            }
        }

        if (_closed || generation != Volatile.Read(ref _displayProfileRefreshGeneration))
        {
            return;
        }

        _currentColorMonitorHandle = resolution.Profile?.MonitorHandle ?? _currentColorMonitorHandle;
        if (string.Equals(
            Environment.GetEnvironmentVariable("FOVIUM_COLOR_DIAGNOSTICS"),
            "1",
            StringComparison.Ordinal))
        {
            var profile = resolution.Profile;
            Console.WriteLine(
                $"Fovium color destination: state={resolution.State}, " +
                $"advancedColor={resolution.AdvancedColorEnabled?.ToString() ?? "unknown"}, " +
                $"bitsPerChannel={resolution.BitsPerColorChannel?.ToString() ?? "unknown"}, " +
                $"profileDescription={profile?.Description ?? "none"}, " +
                $"profileBytes={profile?.Bytes.Length ?? 0}, " +
                $"profileHash={profile?.Identity.DiagnosticPrefix ?? "none"}, vcgt={profile?.HasVcgt ?? false}.");
        }

        PhotoViewport.SetDisplayProfile(resolution);
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
        if (!PhotoPresentationInputPolicy.Allows(
                command,
                PhotoViewport.PhotoPresentationViewEnabled))
        {
            return Task.CompletedTask;
        }

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

    void IViewerCommandTarget.TogglePhotoPresentation()
    {
        _presentation.EndTemporaryHand();
        PhotoViewport.SetPhotoPresentationViewEnabled(
            !PhotoViewport.PhotoPresentationViewEnabled);
    }

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

    void IViewerCommandTarget.ToggleHistogram()
    {
        _histogram.Toggle();
        _histogramFloatingOverlay.ApplyPlacement();
    }

    void IViewerCommandTarget.ToggleColorPicker()
    {
        _colorPicker.Toggle();
        _colorPickerFloatingOverlay.ApplyPlacement();
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
            _histogramFloatingOverlay.ApplyPlacement();
            _colorPickerFloatingOverlay.ApplyPlacement();
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

    private void ConfigureHistogram()
    {
        HistogramTitleText.Text = _localizer[UiStrings.HistogramTitle];
        HistogramCloseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        ToolTip.SetTip(HistogramDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(HistogramCloseButton, _localizer[UiStrings.HistogramClose]);
        HistogramCloseButton.Click += (_, _) =>
        {
            _histogram.SetVisible(false);
            PhotoViewport.Focus();
        };
        HistogramPanel.AddHandler(
            PointerPressedEvent,
            OnMarkupPanelPointerPressed,
            RoutingStrategies.Tunnel);
        ApplyHistogramUi();
    }

    private void OnHistogramStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyHistogramUi();
            return;
        }

        Dispatcher.UIThread.Post(ApplyHistogramUi);
    }

    private void ApplyHistogramUi()
    {
        if (!IsInitialized || _closed)
        {
            return;
        }

        HistogramPanel.IsVisible = _histogram.IsVisible;
        var state = _histogram.CurrentState;
        HistogramPlot.SetState(state?.Data, state?.IsLoading == true);
        if (_histogram.IsVisible)
        {
            _histogramFloatingOverlay.ApplyPlacement();
            Dispatcher.UIThread.Post(
                _histogramFloatingOverlay.ApplyPlacement,
                DispatcherPriority.Loaded);
        }
    }

    private void ConfigureColorPicker()
    {
        ColorPickerTitleText.Text = _localizer[UiStrings.ColorPickerTitle];
        ColorPickerEmptyText.Text = _localizer[UiStrings.ColorPickerEmpty];
        ColorPickerRecentText.Text = _localizer[UiStrings.ColorPickerRecent];
        ColorPickerCloseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        ToolTip.SetTip(ColorPickerDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(ColorPickerCloseButton, _localizer[UiStrings.ColorPickerClose]);
        ColorPickerCloseButton.Click += (_, _) =>
        {
            _colorPicker.SetVisible(false);
            PhotoViewport.Focus();
        };
        ColorPickerPanel.AddHandler(
            PointerPressedEvent,
            OnMarkupPanelPointerPressed,
            RoutingStrategies.Tunnel);
        ApplyColorPickerUi();
    }

    private void OnColorPickerChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyColorPickerUi();
            return;
        }

        Dispatcher.UIThread.Post(ApplyColorPickerUi);
    }

    private void OnColorSampleRequested(object? sender, PhotoSampleRequestedEventArgs e)
    {
        if (!_colorPicker.IsVisible)
        {
            return;
        }

        try
        {
            _colorPicker.Commit(_photoColorSampler.Sample(e.Image.Image, e.OrientedPixel));
        }
        catch (InvalidDataException exception)
        {
            Debug.WriteLine($"Fovium Color Picker catalog failure: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            Debug.WriteLine($"Fovium Color Picker sample failure: {exception.Message}");
        }
    }

    private void ApplyColorPickerUi()
    {
        if (!IsInitialized || _closed)
        {
            return;
        }

        PhotoViewport.SetColorPickerEnabled(_colorPicker.IsVisible);
        ColorPickerPanel.IsVisible = _colorPicker.IsVisible;
        var sample = _colorPicker.CurrentSample;
        ColorPickerEmptyText.IsVisible = sample is null;
        ColorPickerSampleContent.IsVisible = sample is not null;
        ColorPickerHistoryRows.Children.Clear();
        if (sample is null)
        {
            return;
        }

        ColorPickerMainSwatch.Background = CreateSampleBrush(sample);
        ColorPickerMainName.Text = GetSampleName(sample);
        ColorPickerMainHex.Text = FormatSampleCode(sample);
        ColorPickerMainComponents.Text = FormatSampleComponents(sample);
        SetAccuracyToolTip(ColorPickerMainHex, sample);
        foreach (var historySample in _colorPicker.History)
        {
            ColorPickerHistoryRows.Children.Add(CreateColorHistoryRow(historySample));
        }

        _colorPickerFloatingOverlay.ApplyPlacement();
        Dispatcher.UIThread.Post(
            _colorPickerFloatingOverlay.ApplyPlacement,
            DispatcherPriority.Loaded);
    }

    private Control CreateColorHistoryRow(ColorSample sample)
    {
        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 7,
        };
        row.Children.Add(new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(3),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Background = CreateSampleBrush(sample),
        });
        var code = new TextBlock
        {
            Width = 92,
            Text = FormatSampleCode(sample),
            Opacity = 0.88,
        };
        SetAccuracyToolTip(code, sample);
        row.Children.Add(code);
        row.Children.Add(new TextBlock
        {
            Text = GetSampleName(sample),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 155,
        });
        return row;
    }

    private static IBrush CreateSampleBrush(ColorSample sample) => new SolidColorBrush(
        Color.FromArgb(sample.Alpha, sample.Red, sample.Green, sample.Blue));

    private string GetSampleName(ColorSample sample) => sample.IsTransparent
        ? _localizer[UiStrings.ColorPickerTransparent]
        : sample.CanonicalName ?? _localizer[UiStrings.ColorPickerTransparent];

    private static string FormatSampleCode(ColorSample sample) =>
        sample.Accuracy == ColorSampleAccuracy.Approximate ? $"≈ {sample.Hex}" : sample.Hex;

    private string FormatSampleComponents(ColorSample sample) => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        _localizer[sample.Alpha == byte.MaxValue
            ? UiStrings.ColorPickerRgb
            : UiStrings.ColorPickerRgba],
        sample.Red,
        sample.Green,
        sample.Blue,
        sample.Alpha);

    private void SetAccuracyToolTip(Control control, ColorSample sample) =>
        ToolTip.SetTip(
            control,
            sample.Accuracy == ColorSampleAccuracy.Approximate
                ? _localizer[UiStrings.ColorPickerApproximate]
                : null);

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

    private async void OnHistogramPlacementCommitted(FloatingOverlayPlacement placement)
    {
        await PersistOverlayPlacementAsync(
            _settings.Current.Presentation with { HistogramPlacement = placement });
    }

    private async void OnColorPickerPlacementCommitted(FloatingOverlayPlacement placement)
    {
        await PersistOverlayPlacementAsync(
            _settings.Current.Presentation with { ColorPickerPlacement = placement });
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
