using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Fovium.Application;
using Fovium.ColorPicking;
using Fovium.ColorSemantics;
using Fovium.ColorManagement;
using Fovium.Diagnostics;
using Fovium.Histogram;
using Fovium.Home;
using Fovium.Imaging;
using Fovium.Input;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Metadata;
using Fovium.Navigation;
using Fovium.Presentation;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Slideshow;
using Fovium.Stage;
using Fovium.Viewer;
using ViewerNavigationDirection = Fovium.Navigation.NavigationDirection;

namespace Fovium.Views;

internal sealed partial class ViewerWindow : Window, IViewerCommandTarget, ISlideshowNavigator
{
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(1.75);
    private static readonly TimeSpan CursorIdlePollInterval = TimeSpan.FromMilliseconds(250);
    private const double RecentCardStride = 206;

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
    private readonly SlideshowSession _slideshow;
    private readonly HomeViewerState _contentState = new();
    private readonly RecentThumbnailProvider _recentThumbnailProvider = new();
    private readonly HomeRecentCarousel _recentCarousel;
    private readonly PhotoColorSampler _photoColorSampler;
    private readonly ColorSampleNameResolver _colorSampleNameResolver;
    private readonly PerceptualColorNameResolver _perceptualColorNameResolver;
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
    private readonly MenuItem _slideshowMenuItem;
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
    private SlideshowSettings _appliedSlideshowSettings;
    private int _presentedSequenceIndex = -1;
    private readonly RecentNavigationCapture _recentNavigationCapture = new();
    private int _homeDisplayCount;
    private CancellationTokenSource? _homeRefreshCancellation;
    private readonly List<Bitmap> _homeThumbnailBitmaps = [];
    private IReadOnlyList<RecentCardUi> _homeRecentCards = [];

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
            new StageImageRepository(session),
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
        _colorSampleNameResolver = new ColorSampleNameResolver(
            _localizer,
            ColorNameDisplayCatalog.ForLocale(_localizer.Locale));
        _perceptualColorNameResolver = new PerceptualColorNameResolver(_localizer);
        _colorPicker.Changed += OnColorPickerChanged;
        PhotoViewport.ColorSampleRequested += OnColorSampleRequested;
        _markupFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            MarkupToolsPanel,
            settings.Current.Presentation.MarkupDockPlacement,
            _interactionDiagnostics,
            [
                MarkupHandButton,
                MarkupBrushButton,
                MarkupEraserButton,
                MarkupLineButton,
                MarkupRectangleButton,
                MarkupEllipseButton,
                MarkupArrowButton,
                MarkupUndoButton,
                MarkupRedoButton,
                MarkupClearButton,
                MarkupCloseButton,
                MarkupColorButton,
                MarkupStrokeSlider,
                MarkupOpacitySlider,
            ]);
        _photoInfoFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            PhotoInfoPanel,
            settings.Current.Presentation.PhotoInfoPlacement,
            _interactionDiagnostics,
            [PhotoInfoCloseButton]);
        _histogramFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            HistogramPanel,
            settings.Current.Presentation.HistogramPlacement,
            _interactionDiagnostics,
            [HistogramCloseButton]);
        _colorPickerFloatingOverlay = new FloatingOverlayInteraction(
            ViewerRoot,
            ColorPickerPanel,
            settings.Current.Presentation.ColorPickerPlacement,
            _interactionDiagnostics,
            [ColorPickerCloseButton, ColorPickerClearButton, ColorPickerHistoryScroller]);
        _markupFloatingOverlay.PlacementCommitted += OnMarkupPlacementCommitted;
        _photoInfoFloatingOverlay.PlacementCommitted += OnPhotoInfoPlacementCommitted;
        _histogramFloatingOverlay.PlacementCommitted += OnHistogramPlacementCommitted;
        _colorPickerFloatingOverlay.PlacementCommitted += OnColorPickerPlacementCommitted;
        _commandExecutor = new ViewerCommandExecutor(this);
        _inspectionCoordinator = new ViewerInspectionCoordinator(PhotoViewport, session, settings);
        _holdController = new ViewerHoldController(new ViewerHoldActionRouter(
            _inspectionCoordinator,
            new MarkupTemporaryHandHoldAction(_presentation, () => _colorPicker.IsVisible)));
        _appliedSlideshowSettings = settings.Current.Slideshow;
        _slideshow = new SlideshowSession(
            this,
            () => _settings.Current.Slideshow);
        _slideshow.Changed += OnSlideshowChanged;
        PhotoViewport.PresentedImageChanged += OnSlideshowPresentedImageChanged;
        ConfigureMarkupTools();
        ConfigurePhotoInfo();
        ConfigureHistogram();
        ConfigureColorPicker();
        _recentCarousel = new HomeRecentCarousel(
            HomeRecentScroller,
            HomeRecentLeftFade,
            HomeRecentRightFade);
        _recentCarousel.ViewportChanged += OnRecentCarouselViewportChanged;
        ConfigureHome();
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
        _slideshowMenuItem = CreateCommandMenuItem(
            UiStrings.CommandToggleSlideshow,
            ViewerCommand.ToggleSlideshow);
        _slideshowMenuItem.ToggleType = MenuItemToggleType.CheckBox;
        PhotoViewport.PhotoPresentationView.Changed += OnPhotoPresentationViewChanged;
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
        SizeChanged += OnHomeSizeChanged;
        PropertyChanged += OnViewerPropertyChanged;
        KeyDown += OnWindowKeyDown;
        KeyUp += OnWindowKeyUp;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            PhotoViewport.Focus();
            ApplySettings(_settings.Current);
            Screens.Changed += OnDisplayRefreshRequired;
            ScheduleDisplayProfileRefresh(forceProfileRefresh: true);
            ApplyHomeResponsiveLayout(ClientSize);
            if (_startupPaths.Count > 0)
            {
                await OpenPathsAsync(_startupPaths);
            }
            else
            {
                EnterHome(refresh: false);
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
        StopHomeWork();
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
        if (string.Equals(
                Environment.GetEnvironmentVariable("FOVIUM_HOME_DIAGNOSTICS"),
                "1",
                StringComparison.Ordinal))
        {
            var metrics = _recentThumbnailProvider.GetMetrics();
            Console.WriteLine(
                $"Fovium Home thumbnails: requests={metrics.Requests}, prepared={metrics.Prepared}, " +
                $"memoryHits={metrics.MemoryCacheHits}, failures={metrics.Failures}, " +
                $"canceled={metrics.Cancellations}, cacheItems={metrics.CachedItems}, " +
                $"retainedBytes={metrics.RetainedBytes}, lastMs={metrics.LastDuration.TotalMilliseconds:F2}, " +
                $"totalPrepareMs={metrics.TotalPreparationDuration.TotalMilliseconds:F2}.");
        }

        _recentCarousel.Dispose();
        _recentThumbnailProvider.Dispose();
        DisposeHomeThumbnailBitmaps();
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

        if (string.Equals(
                Environment.GetEnvironmentVariable("FOVIUM_SLIDESHOW_DIAGNOSTICS"),
                "1",
                StringComparison.Ordinal))
        {
            var metrics = _slideshow.Metrics;
            var preparedBytes = PhotoViewport.MonitorColorMetrics?.CurrentRasterBytes ?? 0;
            Console.WriteLine(
                $"Fovium slideshow: running={metrics.Running}, quiescent={metrics.Quiescent}, " +
                $"starts={metrics.Starts}, stops={metrics.Stops}, naturalStops={metrics.NaturalStops}, " +
                $"loops={metrics.Loops}, expirations={metrics.TimerExpirations}, " +
                $"manualResets={metrics.ManualNavigationResets}, presented={metrics.PresentedSlideCount}, " +
                $"preparedHits={metrics.PreparedNextHits}, misses={metrics.PreparedNextMisses}, " +
                $"rejectedByMemory={metrics.PreparedNextRejectedByMemory}, stale={metrics.PreparedNextStale}.");
            Console.WriteLine(
                $"Fovium slideshow timing/memory: lastPresentedMs={metrics.LastPresentedDuration.TotalMilliseconds:F2}, " +
                $"lastTransitionWaitMs={metrics.LastTransitionWait.TotalMilliseconds:F2}, " +
                $"lastPreparedBytes={metrics.LastPreparedManagedBytes}, " +
                $"currentManagedBytes={PhotoViewport.CurrentManagedSourceBytes}, " +
                $"coordinatorPreparedBytes={preparedBytes}, " +
                $"currentPlusPreparedBytes={PhotoViewport.CurrentManagedSourceBytes + preparedBytes}.");
        }

        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
        _handCursor.Dispose();
        _settings.SettingsChanged -= OnSettingsChanged;
        PhotoViewport.PhotoPresentationView.Changed -= OnPhotoPresentationViewChanged;
        PhotoViewport.PresentedImageChanged -= OnSlideshowPresentedImageChanged;
        _slideshow.Changed -= OnSlideshowChanged;
        _slideshow.Dispose();
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
        PropertyChanged -= OnViewerPropertyChanged;
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                switch (ViewerEscapePolicy.Resolve(
                            _holdController.Cancel(),
                            _slideshow.IsRunning,
                            WindowState == WindowState.FullScreen))
                {
                    case ViewerEscapeAction.None:
                        return;
                    case ViewerEscapeAction.StopSlideshow:
                        _slideshow.Stop();
                        return;
                    case ViewerEscapeAction.LeaveFullscreen:
                        LeaveFullscreen();
                        return;
                    case ViewerEscapeAction.CloseViewer:
                        Close();
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
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
        var closePhoto = CreateCommandMenuItem(
            UiStrings.CommandClosePhoto,
            ViewerCommand.ClosePhoto,
            FoviumIcon.Close);
        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                CreateCommandMenuItem(UiStrings.MenuOpen, ViewerCommand.Open, FoviumIcon.Open),
                closePhoto,
                new Separator(),
                _previousMenuItem,
                _nextMenuItem,
                new Separator(),
                CreateCommandMenuItem(UiStrings.MenuFit, ViewerCommand.Fit, FoviumIcon.Fit),
                CreateCommandMenuItem(
                    UiStrings.MenuActualSize,
                    ViewerCommand.ActualSize,
                    FoviumIcon.ActualSize),
                _slideshowMenuItem,
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
                new Separator(),
                CreateMenuItem(UiStrings.MenuExitFovium, () =>
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
            closePhoto.IsVisible = _contentState.Mode == ViewerContentMode.Viewer;
            closePhoto.IsEnabled = closePhoto.IsVisible;
            foreach (var (mode, item) in _stageBackgroundMenuItems)
            {
                item.IsChecked = _settings.Current.Stage.BackgroundMode == mode;
            }

            _matteMenuItem.IsChecked = _settings.Current.Stage.MatteEnabled;
            _slideshowMenuItem.IsChecked = _slideshow.IsRunning;
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
            [StageBackgroundMode.Average] = UiStrings.StageAverage,
            [StageBackgroundMode.Dominant] = UiStrings.StageDominant,
            [StageBackgroundMode.ColorWash] = UiStrings.StageColorWash,
            [StageBackgroundMode.ColorGradient] = UiStrings.StageColorGradient,
            [StageBackgroundMode.SoftGlow] = UiStrings.StageSoftGlow,
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

    private async Task OpenFolderFromPickerAsync()
    {
        if (_closed)
        {
            return;
        }

        var selected = await PickFolderAsync();
        if (selected is not null)
        {
            await OpenActivationAsync(ActivationPlan.CreateFolder(selected));
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

    private void ConfigureHome()
    {
        HomeTitleText.Text = _localizer[UiStrings.HomeTitle];
        HomeSubtitleText.Text = _localizer[UiStrings.HomeSubtitle];
        HomeDropHintText.Text = _localizer[UiStrings.HomeDropHint];
        HomeShortcutActionText.Text = _localizer[UiStrings.HomeShortcutAction];
        HomeRecentTitleText.Text = _localizer[UiStrings.HomeRecent];
        HomeClearRecentButton.Content = _localizer[UiStrings.HomeClearRecent];
        HomeOpenFilesButton.Content = CreateHomeActionContent(
            FoviumIcon.Photo,
            _localizer[UiStrings.HomeOpenFiles]);
        HomeOpenFolderButton.Content = CreateHomeActionContent(
            FoviumIcon.Folder,
            _localizer[UiStrings.HomeOpenFolder]);
        HomeSettingsButton.Content = FoviumIconCatalog.Create(FoviumIcon.Settings, 17);
        ToolTip.SetTip(HomeSettingsButton, _localizer[UiStrings.MenuSettings]);
        AutomationProperties.SetName(HomeOpenFilesButton, _localizer[UiStrings.HomeOpenFiles]);
        AutomationProperties.SetName(HomeOpenFolderButton, _localizer[UiStrings.HomeOpenFolder]);
        AutomationProperties.SetName(HomeSettingsButton, _localizer[UiStrings.MenuSettings]);
        AutomationProperties.SetName(HomeClearRecentButton, _localizer[UiStrings.HomeClearRecent]);
        DropOverlayText.Text = _localizer[UiStrings.HomeDropReady];
        DropOverlayIcon.Data = StreamGeometry.Parse(
            "M8,1 L8,10 M4,6 L8,10 L12,6 M2,12 L2,15 L14,15 L14,12");

        HomeOpenFilesButton.Click += async (_, _) =>
            await RunBoundaryActionAsync(OpenFromPickerAsync);
        HomeOpenFolderButton.Click += async (_, _) =>
            await RunBoundaryActionAsync(OpenFolderFromPickerAsync);
        HomeSettingsButton.Click += (_, _) => ShowSettings();
        HomeClearRecentButton.Click += async (_, _) =>
            await RunBoundaryActionAsync(() =>
                _settings.ClearRecentLocationsAsync(_lifetimeCancellation.Token));

        DragDrop.SetAllowDrop(ViewerRoot, true);
        DragDrop.AddDragEnterHandler(ViewerRoot, OnViewerDragEnter);
        DragDrop.AddDragOverHandler(ViewerRoot, OnViewerDragOver);
        DragDrop.AddDragLeaveHandler(ViewerRoot, OnViewerDragLeave);
        DragDrop.AddDropHandler(ViewerRoot, OnViewerDrop);
    }

    private void OnHomeSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ApplyHomeResponsiveLayout(e.NewSize);

    private void ApplyHomeResponsiveLayout(Size size)
    {
        var compact = size.Width < 640 || size.Height < 540;
        HomeHeroVisual.IsVisible = !compact;
        HomeHeroLayout.RowDefinitions[0].Height = new GridLength(compact ? 0 : 132);
        HomeHeroCard.MinHeight = compact ? 0 : 370;
        HomeHeroCard.Padding = compact
            ? new Thickness(16, 16, 16, 16)
            : new Thickness(26, 22, 26, 23);
        HomeTitleText.FontSize = compact ? 28 : 35;
        HomeComposition.Margin = compact
            ? new Thickness(14, 48, 14, 14)
            : new Thickness(24, 58, 24, 26);
        HomeRecentSection.Margin = compact
            ? new Thickness(0, 12, 0, 0)
            : new Thickness(0, 20, 0, 0);
    }

    private static StackPanel CreateHomeActionContent(FoviumIcon icon, string text)
    {
        var content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 9,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
        content.Children.Add(FoviumIconCatalog.Create(icon, 18));
        content.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        return content;
    }

    private void ApplyHomeSettings(HomeSettings home, ShortcutSettings shortcuts)
    {
        var refreshStarted = Stopwatch.GetTimestamp();
        var openShortcut = shortcuts.Get(ViewerCommand.Open);
        HomeShortcutHint.IsVisible = openShortcut is not null;
        if (openShortcut is not null)
        {
            HomeShortcutGestureText.Text = ShortcutGestureFormatter.Format(openShortcut, string.Empty);
        }

        StopHomeRefresh();
        DisposeHomeThumbnailBitmaps();
        HomeRecentItemsPanel.Children.Clear();
        _homeRecentCards = [];
        var showRecent = home.RememberRecentPhotos && home.RecentLocations.Count > 0;
        HomeRecentSection.IsVisible = showRecent;
        if (!showRecent)
        {
            _recentCarousel.Refresh();
            TraceHomeDisplay(refreshStarted, 0);
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);
        _homeRefreshCancellation = cancellation;
        var cards = home.RecentLocations
            .Select(CreateRecentLocationCard)
            .ToArray();
        _homeRecentCards = cards;
        foreach (var card in cards)
        {
            HomeRecentItemsPanel.Children.Add(card.Button);
        }

        _recentCarousel.Refresh();
        _ = RefreshRecentAvailabilitiesAsync(cards, cancellation.Token);
        QueueVisibleRecentThumbnails(0, HomeRecentScroller.Viewport.Width);
        TraceHomeDisplay(refreshStarted, cards.Length);
    }

    private void TraceHomeDisplay(long started, int recentCount)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("FOVIUM_HOME_DIAGNOSTICS"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        Console.WriteLine(
            $"Fovium Home display: ordinal={++_homeDisplayCount}, recent={recentCount}, " +
            $"syncMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F2}.");
    }

    private RecentCardUi CreateRecentLocationCard(RecentLocation location)
    {
        var fileName = location.Kind == RecentLocationKind.File
            ? Path.GetFileName(location.Path)
            : new DirectoryInfo(location.Path).Name;
        var title = string.IsNullOrWhiteSpace(fileName) ? location.Path : fileName;
        var kindText = _localizer[location.Kind == RecentLocationKind.Folder
            ? UiStrings.HomeRecentFolder
            : UiStrings.HomeRecentFile];
        var pathText = location.Kind == RecentLocationKind.File
            ? Path.GetDirectoryName(location.Path) ?? location.Path
            : location.Path;
        var placeholder = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#382C4C"), 0),
                    new GradientStop(Color.Parse("#1B2533"), 1),
                },
            },
            Child = FoviumIconCatalog.Create(
                location.Kind == RecentLocationKind.Folder ? FoviumIcon.Folder : FoviumIcon.Photo,
                26),
        };
        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            IsVisible = false,
        };
        var preview = new Grid
        {
            Height = 104,
            ClipToBounds = true,
            Children = { placeholder, image },
        };
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var metadata = new TextBlock
        {
            Text = $"{kindText}  ·  {pathText}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#9D95A7")),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var labels = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(10, 6, 35, 7),
            Children = { titleText, metadata },
        };
        var action = new Button
        {
            Content = "⋯",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 5, 5),
        };
        action.Classes.Add("recent-item-action");
        AutomationProperties.SetName(action, _localizer[UiStrings.HomeRecentActions]);
        ToolTip.SetTip(action, _localizer[UiStrings.HomeRecentActions]);
        var removeMenu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                CreateMenuItem(UiStrings.HomeRemoveRecent, () =>
                    _settings.RemoveRecentLocationAsync(
                        location.Path,
                        _lifetimeCancellation.Token), FoviumIcon.Close),
            },
        };
        action.Click += (_, e) =>
        {
            e.Handled = true;
            removeMenu.Open(action);
        };
        var details = new Grid { Children = { labels, action } };
        var content = new Grid { RowDefinitions = new RowDefinitions("104,*") };
        content.Children.Add(preview);
        Grid.SetRow(details, 1);
        content.Children.Add(details);

        var button = new Button { Content = content };
        button.Classes.Add("recent-card");
        AutomationProperties.SetName(button, $"{kindText}: {title}");
        ToolTip.SetTip(button, location.Path);
        var card = new RecentCardUi(location, button, image, placeholder, metadata);
        button.Click += async (_, _) => await ActivateRecentCardAsync(card);
        button.GotFocus += (_, _) => button.BringIntoView();
        return card;
    }

    private async Task RefreshRecentAvailabilitiesAsync(
        IReadOnlyList<RecentCardUi> cards,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(cards.Select(card =>
                RefreshRecentAvailabilityAsync(card, cancellationToken)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshRecentAvailabilityAsync(
        RecentCardUi card,
        CancellationToken cancellationToken)
    {
        var availability = await Task.Run(
            () => RecentLocationAvailability.Resolve(card.Location, File.Exists, Directory.Exists),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var kindText = _localizer[card.Location.Kind == RecentLocationKind.Folder
            ? UiStrings.HomeRecentFolder
            : UiStrings.HomeRecentFile];
        card.Metadata.Text = availability == RecentAvailability.Unavailable
            ? $"{kindText}  ·  {_localizer[UiStrings.HomeRecentUnavailable]}"
            : kindText;
        card.Button.Classes.Set("unavailable", availability == RecentAvailability.Unavailable);
    }

    private void OnRecentCarouselViewportChanged(double offset, double viewportWidth) =>
        QueueVisibleRecentThumbnails(offset, viewportWidth);

    private void QueueVisibleRecentThumbnails(double offset, double viewportWidth)
    {
        var root = _homeRefreshCancellation;
        var cards = _homeRecentCards;
        if (root is null || root.IsCancellationRequested || cards.Count == 0)
        {
            return;
        }

        var window = RecentThumbnailLoadingPolicy.Resolve(
            offset,
            viewportWidth,
            RecentCardStride,
            cards.Count);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (!window.Contains(index))
            {
                card.ThumbnailCancellation?.Cancel();
                continue;
            }

            if (card.ThumbnailReady ||
                card.ThumbnailCancellation is not null ||
                string.IsNullOrWhiteSpace(card.Location.PreviewPath))
            {
                continue;
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(root.Token);
            card.ThumbnailCancellation = cancellation;
            _ = RefreshRecentThumbnailAsync(card, cancellation);
        }
    }

    private async Task RefreshRecentThumbnailAsync(
        RecentCardUi card,
        CancellationTokenSource cancellation)
    {
        var retryAfterCancellation = false;
        try
        {
            var thumbnail = await PrepareRecentThumbnailAsync(
                card.Location.PreviewPath!,
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (thumbnail.Status != RecentThumbnailStatus.Ready || thumbnail.PngBytes is null)
            {
                return;
            }

            using var stream = new MemoryStream(thumbnail.PngBytes, writable: false);
            Bitmap? bitmap = new(stream);
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                _homeThumbnailBitmaps.Add(bitmap);
                card.Image.Source = bitmap;
                card.Image.IsVisible = true;
                card.Placeholder.IsVisible = false;
                card.ThumbnailReady = true;
                bitmap = null;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            retryAfterCancellation = true;
        }
        finally
        {
            if (ReferenceEquals(card.ThumbnailCancellation, cancellation))
            {
                card.ThumbnailCancellation = null;
            }

            cancellation.Dispose();
            if (retryAfterCancellation && _homeRefreshCancellation is { IsCancellationRequested: false })
            {
                QueueVisibleRecentThumbnails(
                    HomeRecentScroller.Offset.X,
                    HomeRecentScroller.Viewport.Width);
            }
        }
    }

    private async Task<RecentThumbnailResult> PrepareRecentThumbnailAsync(
        string path,
        CancellationToken cancellationToken) =>
        await _recentThumbnailProvider.PrepareAsync(
            path,
            RecentThumbnailProvider.TargetLongEdge,
            cancellationToken);

    private async Task ActivateRecentCardAsync(RecentCardUi card)
    {
        if (_recentCarousel.ConsumeSuppressedActivation())
        {
            return;
        }

        _recentCarousel.Stop();
        var availability = RecentLocationAvailability.Resolve(
            card.Location,
            File.Exists,
            Directory.Exists);
        if (availability == RecentAvailability.Unavailable)
        {
            card.Button.Classes.Set("unavailable", true);
            var kindText = _localizer[card.Location.Kind == RecentLocationKind.Folder
                ? UiStrings.HomeRecentFolder
                : UiStrings.HomeRecentFile];
            card.Metadata.Text = $"{kindText}  ·  {_localizer[UiStrings.HomeRecentUnavailable]}";
            return;
        }

        await RunBoundaryActionAsync(() => OpenActivationAsync(
            card.Location.Kind == RecentLocationKind.Folder
                ? ActivationPlan.CreateFolder(card.Location.Path)
                : ActivationPlan.Create([card.Location.Path])));
        if (_contentState.Mode == ViewerContentMode.Home &&
            RecentLocationAvailability.Resolve(
                card.Location,
                File.Exists,
                Directory.Exists) == RecentAvailability.Unavailable)
        {
            ErrorSurface.IsVisible = false;
            card.Button.Classes.Set("unavailable", true);
            var kindText = _localizer[card.Location.Kind == RecentLocationKind.Folder
                ? UiStrings.HomeRecentFolder
                : UiStrings.HomeRecentFile];
            card.Metadata.Text =
                $"{kindText}  ·  {_localizer[UiStrings.HomeRecentBecameUnavailable]}";
        }
    }

    private void OnViewerDragEnter(object? sender, DragEventArgs e) => UpdateDropState(e);

    private void OnViewerDragOver(object? sender, DragEventArgs e) => UpdateDropState(e);

    private void UpdateDropState(DragEventArgs e)
    {
        var plan = TryCreateDropPlan(e.DataTransfer);
        e.DragEffects = plan is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        DropOverlay.IsVisible = plan is not null;
        HomeHeroCard.Classes.Set("drag-ready", plan is not null && HomeSurface.IsVisible);
    }

    private void OnViewerDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
        HomeHeroCard.Classes.Set("drag-ready", false);
        e.Handled = true;
    }

    private async void OnViewerDrop(object? sender, DragEventArgs e)
    {
        var plan = TryCreateDropPlan(e.DataTransfer);
        DropOverlay.IsVisible = false;
        HomeHeroCard.Classes.Set("drag-ready", false);
        e.Handled = true;
        if (plan is not null)
        {
            await RunBoundaryActionAsync(() => OpenActivationAsync(plan));
        }
    }

    private static ActivationPlan? TryCreateDropPlan(IDataTransfer dataTransfer)
    {
        var paths = dataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray() ?? [];
        return ActivationPlan.CreateDrop(paths);
    }

    private async Task RunBoundaryActionAsync(Func<Task> action)
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
    }

    private void ShowHomeError(string key)
    {
        ErrorText.Text = _localizer[key];
        ErrorSurface.IsVisible = true;
    }

    private async Task<string?> PickFolderAsync()
    {
        ShowCursor();
        _cursorTimer.Stop();
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = _localizer[UiStrings.PickerFolderTitle],
                AllowMultiple = false,
            });
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
        finally
        {
            RestartCursorTimer();
        }
    }

    private Task OpenPathsAsync(IReadOnlyList<string> paths) =>
        OpenActivationAsync(ActivationPlan.Create(paths));

    private async Task OpenActivationAsync(ActivationPlan plan)
    {
        var ticket = _contentState.BeginOpen();
        _slideshow.Stop();
        _holdController.Cancel();
        CompleteAmbientSoakTransition();
        ErrorSurface.IsVisible = false;
        var sequence = await _activation.ResolveAsync(plan, _lifetimeCancellation.Token);
        if (sequence is null)
        {
            if (plan.Mode == ActivationMode.Folder)
            {
                ShowHomeError(UiStrings.ErrorFolderEmpty);
            }

            return;
        }

        var result = await _session.OpenAsync(sequence, _lifetimeCancellation.Token);
        if (result.Status == SelectionStatus.Published && !_contentState.TryPublish(ticket))
        {
            result.Image?.Dispose();
            return;
        }

        _presentation.StartNewSequence();
        _photoInfo.BeginNewSequence();
        _histogram.BeginNewSequence();
        ApplySelection(result, ImageChangeViewPolicyResolver.ForNewSequence(), showFailure: true);
        if (result.Status == SelectionStatus.Published &&
            result.Path is not null &&
            RecentCapturePolicyResolver.ShouldCapture(
                _settings.Current.Home,
                RecentCaptureTrigger.ExplicitOpen))
        {
            var recent = plan.Mode == ActivationMode.Folder
                ? new RecentLocation
                {
                    Kind = RecentLocationKind.Folder,
                    Path = plan.Paths[0],
                    PreviewPath = result.Path,
                }
                : new RecentLocation { Kind = RecentLocationKind.File, Path = result.Path };
            await _settings.AddRecentLocationAsync(recent, _lifetimeCancellation.Token);
        }
    }

    private async Task NavigateAsync(ViewerNavigationDirection direction)
    {
        var slideshowNavigationGeneration = _slideshow.NotifyManualNavigationStarted();
        _holdController.Cancel();
        CompleteAmbientSoakTransition();
        var result = await _session.NavigateAsync(direction, _lifetimeCancellation.Token);
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            _settings.Current.ImageChangeViewPolicy,
            PhotoViewport.CaptureViewTransfer());
        if (result.Status == SelectionStatus.Published &&
            result.Path is { } publishedPath &&
            RecentCapturePolicyResolver.ShouldCapture(
                _settings.Current.Home,
                RecentCaptureTrigger.ManualNavigation))
        {
            _recentNavigationCapture.Arm(publishedPath);
        }
        else
        {
            _recentNavigationCapture.Cancel();
        }

        ApplySelection(result, transfer, showFailure: false);
        if (result.Status != SelectionStatus.Published)
        {
            _slideshow.NotifyManualNavigationCompletedWithoutPresentation(
                slideshowNavigationGeneration);
        }
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
            StopHomeWork();
            ErrorSurface.IsVisible = false;
            HomeSurface.IsVisible = false;
            RestartCursorTimer();
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
        _contentState.TryReturnHome();
        EnterHome();
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

    private async Task ClosePhotoAsync()
    {
        if (!_contentState.TryReturnHome())
        {
            return;
        }

        _slideshow.Stop();
        _holdController.Cancel();
        CompleteAmbientSoakTransition();
        PhotoViewport.CancelPendingPresentation();
        await _session.CloseAsync();
        PhotoViewport.ClearImage();
        PhotoViewport.SetPhotoPresentationViewEnabled(false);
        _stageCoordinator.ClearImage();
        _presentation.DeactivateForHome();
        _photoInfo.SetVisible(false);
        _photoInfo.BeginNewSequence();
        _histogram.SetVisible(false);
        _histogram.BeginNewSequence();
        _colorPicker.SetVisible(false);
        _colorPicker.ClearHistory();
        _presentedSequenceIndex = -1;
        _recentNavigationCapture.Cancel();
        ErrorSurface.IsVisible = false;
        DropOverlay.IsVisible = false;
        HomeHeroCard.Classes.Set("drag-ready", false);
        EnterHome();
        HomeOpenFilesButton.Focus();
    }

    private void EnterHome(bool refresh = true)
    {
        HomeSurface.IsVisible = true;
        ShowCursor();
        _cursorTimer.Stop();
        if (refresh)
        {
            ApplyHomeSettings(_settings.Current.Home, _settings.Current.Shortcuts);
        }
    }

    private void StopHomeWork()
    {
        StopHomeRefresh();
        _recentCarousel.Stop();
        DisposeHomeThumbnailBitmaps();
    }

    private void StopHomeRefresh()
    {
        _homeRefreshCancellation?.Cancel();
        _homeRefreshCancellation?.Dispose();
        _homeRefreshCancellation = null;
        _homeRecentCards = [];
    }

    private void DisposeHomeThumbnailBitmaps()
    {
        foreach (var bitmap in _homeThumbnailBitmaps)
        {
            bitmap.Dispose();
        }

        _homeThumbnailBitmaps.Clear();
    }

    private sealed class RecentCardUi(
        RecentLocation location,
        Button button,
        Image image,
        Control placeholder,
        TextBlock metadata)
    {
        public RecentLocation Location { get; } = location;

        public Button Button { get; } = button;

        public Image Image { get; } = image;

        public Control Placeholder { get; } = placeholder;

        public TextBlock Metadata { get; } = metadata;

        public CancellationTokenSource? ThumbnailCancellation { get; set; }

        public bool ThumbnailReady { get; set; }
    }

    private void ShowBoundaryError()
    {
        if (_closed)
        {
            return;
        }

        _holdController.Cancel();
        PhotoViewport.ClearImage();
        _stageCoordinator.ClearImage();
        _contentState.TryReturnHome();
        EnterHome();
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

        var preferredSize = _settings.Current.SettingsWindowSize;
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var initialSize = screen is null
            ? new Size(preferredSize.WidthDip, preferredSize.HeightDip)
            : SettingsWindowSizePolicy.Resolve(
                preferredSize,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                screen.Scaling);
        var window = new SettingsWindow(
            _settings,
            _localizer,
            PhotoViewport.PhotoPresentationView,
            _slideshow,
            initialSize);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            RestartCursorTimer();
        };
        window.Show(this);
        window.Activate();
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty && _settingsWindow is { } settingsWindow)
        {
            Dispatcher.UIThread.Post(settingsWindow.Activate);
        }
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
        var previousSlideshow = _appliedSlideshowSettings;
        _appliedSlideshowSettings = settings.Slideshow;
        ApplyStage(settings.Stage);
        PhotoViewport.SetPhotoPresentationViewSettings(settings.PhotoPresentationView);
        _presentation.ApplySettings(settings.Presentation);
        ApplyMarkupToolsUi();
        UpdateMarkupToolTips();
        _markupFloatingOverlay.SetPlacement(settings.Presentation.MarkupDockPlacement);
        _photoInfoFloatingOverlay.SetPlacement(settings.Presentation.PhotoInfoPlacement);
        _histogramFloatingOverlay.SetPlacement(settings.Presentation.HistogramPlacement);
        _colorPickerFloatingOverlay.SetPlacement(settings.Presentation.ColorPickerPlacement);
        if (!settings.Home.RememberRecentPhotos)
        {
            StopHomeRefresh();
            DisposeHomeThumbnailBitmaps();
            _recentThumbnailProvider.ClearMemoryCache();
        }

        if (_contentState.Mode == ViewerContentMode.Home)
        {
            ApplyHomeSettings(settings.Home, settings.Shortcuts);
        }

        if (_appliedMonitorColorManagementEnabled != settings.MonitorColorManagementEnabled)
        {
            _appliedMonitorColorManagementEnabled = settings.MonitorColorManagementEnabled;
            PhotoViewport.SetMonitorColorManagementEnabled(settings.MonitorColorManagementEnabled);
            ScheduleDisplayProfileRefresh(forceProfileRefresh: true);
            _slideshow.NotifyDestinationChanged();
        }


        if (previousSlideshow.SlideDurationSeconds != settings.Slideshow.SlideDurationSeconds)
        {
            _slideshow.NotifyDurationChanged();
        }

        if (previousSlideshow.EndBehavior != settings.Slideshow.EndBehavior)
        {
            _slideshow.NotifyEndBehaviorChanged();
        }
    }

    private void OnPhotoPresentationViewChanged(object? sender, EventArgs e)
    {
        _photoPresentationMenuItem.IsChecked = PhotoViewport.PhotoPresentationViewEnabled;
        _commandMenuItems[ViewerCommand.Fit].IsEnabled =
            !PhotoViewport.PhotoPresentationViewEnabled;
        _commandMenuItems[ViewerCommand.ActualSize].IsEnabled =
            !PhotoViewport.PhotoPresentationViewEnabled;
    }

    private void OnSlideshowChanged(object? sender, EventArgs e) =>
        _slideshowMenuItem.IsChecked = _slideshow.IsRunning;

    private void OnSlideshowPresentedImageChanged(object? sender, EventArgs e)
    {
        _presentedSequenceIndex = _session.CurrentIndex;
        if (_recentNavigationCapture.TryConsume(
                PhotoViewport.PresentedImageIdentity,
                PhotoViewport.InspectionMode != InspectionMode.None,
                out var recentPath))
        {
            _ = RecordPresentedNavigationRecentAsync(recentPath);
        }

        if (((ISlideshowNavigator)this).PresentedSlide is { } presented)
        {
            _slideshow.NotifyPresented(presented);
        }
    }

    private async Task RecordPresentedNavigationRecentAsync(string path)
    {
        if (!RecentCapturePolicyResolver.ShouldCapture(
                _settings.Current.Home,
                RecentCaptureTrigger.ManualNavigation))
        {
            return;
        }

        try
        {
            await _settings.AddRecentLocationAsync(
                new RecentLocation { Kind = RecentLocationKind.File, Path = path },
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
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

        if (PhotoViewport.SetDisplayProfile(resolution))
        {
            _slideshow.NotifyDestinationChanged();
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
        if (_contentState.Mode == ViewerContentMode.Viewer)
        {
            EnsureCursorTimerRunning();
        }
    }

    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        var hasActivity = _lastPointerActivityTimestamp != 0;
        var idle = hasActivity
            ? Stopwatch.GetElapsedTime(_lastPointerActivityTimestamp)
            : TimeSpan.Zero;
        if (HomeCursorPolicy.ShouldHideViewerCursor(
                _contentState.Mode,
                _contextMenuOpen,
                hasActivity,
                idle,
                CursorHideDelay))
        {
            PhotoViewport.SetViewerCursor(_hiddenCursor);
        }
    }

    private void ShowCursor() => PhotoViewport.SetViewerCursor(_visibleCursor);

    private void RestartCursorTimer()
    {
        if (!HomeCursorPolicy.ShouldRunViewerIdleTimer(
                _contentState.Mode,
                _lifetimeCancellation.IsCancellationRequested,
                _contextMenuOpen))
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

    void IViewerCommandTarget.ToggleSlideshow() => _slideshow.Toggle();

    void IViewerCommandTarget.TogglePhotoPresentation()
    {
        _presentation.EndTemporaryHand();
        PhotoViewport.PhotoPresentationView.Toggle();
    }

    bool ISlideshowNavigator.IsNavigationPending =>
        _session.IsNavigationPending ||
        (_presentedSequenceIndex >= 0 && _presentedSequenceIndex != _session.CurrentIndex);

    SlideshowPresentedSlide? ISlideshowNavigator.PresentedSlide
    {
        get
        {
            if (!PhotoViewport.TryAcquirePresentedImage(out var presented) || presented is null)
            {
                return null;
            }

            using (presented)
            {
                return new SlideshowPresentedSlide(
                    _presentedSequenceIndex >= 0 ? _presentedSequenceIndex : _session.CurrentIndex,
                    presented.ImageIdentity,
                    presented.PresentationIdentity);
            }
        }
    }

    async Task<SlideshowPreparationResult> ISlideshowNavigator.PrepareNextAsync(
        SlideshowPresentedSlide expectedCurrent,
        SlideshowEndBehavior endBehavior,
        CancellationToken cancellationToken)
    {
        var current = ((ISlideshowNavigator)this).PresentedSlide;
        if (current != expectedCurrent || _session.IsNavigationPending)
        {
            return new SlideshowPreparationResult(SlideshowPreparationStatus.Stale);
        }

        var acquired = await _session.AcquireNeighborForInspectionAsync(
            ViewerNavigationDirection.Next,
            wrap: endBehavior == SlideshowEndBehavior.Loop,
            cancellationToken);
        if (acquired.Status != InspectionAcquisitionStatus.Acquired || acquired.Image is null)
        {
            acquired.Image?.Dispose();
            return new SlideshowPreparationResult(
                acquired.Status is InspectionAcquisitionStatus.Canceled or
                    InspectionAcquisitionStatus.Stale
                    ? SlideshowPreparationStatus.Stale
                    : SlideshowPreparationStatus.NoOtherViableImage);
        }

        using (acquired.Image)
        {
            if (((ISlideshowNavigator)this).PresentedSlide != expectedCurrent ||
                acquired.Index == expectedCurrent.SequenceIndex)
            {
                return new SlideshowPreparationResult(SlideshowPreparationStatus.Stale);
            }

            return await PhotoViewport.PrepareSlideshowNextAsync(
                acquired.Image,
                cancellationToken);
        }
    }

    async Task<SlideshowAdvanceStatus> ISlideshowNavigator.AdvanceAsync(
        SlideshowPresentedSlide expectedCurrent,
        SlideshowEndBehavior endBehavior,
        CancellationToken cancellationToken)
    {
        if (((ISlideshowNavigator)this).PresentedSlide != expectedCurrent)
        {
            return SlideshowAdvanceStatus.Canceled;
        }

        var result = await _session.NavigateAsync(
            ViewerNavigationDirection.Next,
            wrap: endBehavior == SlideshowEndBehavior.Loop,
            cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            result.Image?.Dispose();
            _session.CancelPendingAndReanchor(expectedCurrent.SequenceIndex);
            return SlideshowAdvanceStatus.Canceled;
        }

        if (result.Status != SelectionStatus.Published || result.Image is null)
        {
            result.Image?.Dispose();
            return result.Status is SelectionStatus.NoMove or SelectionStatus.NoViableCandidate
                ? SlideshowAdvanceStatus.NoOtherViableImage
                : SlideshowAdvanceStatus.Canceled;
        }

        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            _settings.Current.ImageChangeViewPolicy,
            PhotoViewport.CaptureViewTransfer());
        ApplySelection(result, transfer, showFailure: false);
        return SlideshowAdvanceStatus.PresentationPending;
    }

    void ISlideshowNavigator.CancelAutomaticAdvance(SlideshowPresentedSlide presentedSlide)
    {
        PhotoViewport.CancelPendingPresentation();
        _session.CancelPendingAndReanchor(presentedSlide.SequenceIndex);
    }

    Task IViewerCommandTarget.ToggleMatteAsync() =>
        _settings.ToggleMatteAsync(_lifetimeCancellation.Token);

    void IViewerCommandTarget.ToggleFullscreen() => ToggleFullscreen();

    Task IViewerCommandTarget.OpenAsync() => OpenFromPickerAsync();

    Task IViewerCommandTarget.ClosePhotoAsync() => ClosePhotoAsync();

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
        MarkupCloseButton.Click += (_, _) =>
        {
            _presentation.CloseMarkupTools();
            PhotoViewport.Focus();
        };
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
        MarkupCloseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        MarkupStrokeText.Text = _localizer[UiStrings.PresentationStroke];
        MarkupOpacityText.Text = _localizer[UiStrings.PresentationOpacity];
        ToolTip.SetTip(MarkupDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(MarkupColorButton, _localizer[UiStrings.PresentationColor]);
        ToolTip.SetTip(MarkupCloseButton, _localizer[UiStrings.PresentationCloseMarkup]);
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
        var editor = new ColorPickerWindow(
            new StageColor(original.Red, original.Green, original.Blue),
            _localizer,
            _localizer[UiStrings.PresentationMarkupColor]);
        editor.ColorChanged += (_, args) => _presentation.SetActiveColor(
            new PresentationColor(args.Color.Red, args.Color.Green, args.Color.Blue));
        var accepted = await editor.ShowDialog<bool>(this);
        var resolved = editor.Resolve(accepted);
        _presentation.SetActiveColor(new PresentationColor(resolved.Red, resolved.Green, resolved.Blue));
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
            MarkupColorButton.SwatchBrush = new SolidColorBrush(
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
            PhotoInfoRows.Children.Clear();
            return;
        }

        var culture = System.Globalization.CultureInfo.GetCultureInfo(
            _localizer.Locale == "ru" ? "ru-RU" : "en-US");
        var text = PhotoInfoFormatter.Format(state, culture, _localizer.Get);
        PhotoInfoRows.Children.Clear();
        AddPhotoColorProfile(state.ColorProfile, culture);
        AddPhotoInfoRow(UiStrings.PhotoInfoCamera, text.Camera, UiStrings.PhotoInfoCameraTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoLens, text.Lens, UiStrings.PhotoInfoLensTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoFocalLength, text.FocalLength, UiStrings.PhotoInfoFocalLengthTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoAperture, text.Aperture, UiStrings.PhotoInfoApertureTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoShutter, text.Shutter, UiStrings.PhotoInfoShutterTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoIso, text.Iso, UiStrings.PhotoInfoIsoTip);
        AddPhotoInfoRow(
            UiStrings.PhotoInfoExposureCompensation,
            text.ExposureCompensation,
            UiStrings.PhotoInfoExposureCompensationTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoMetering, text.MeteringMode, UiStrings.PhotoInfoMeteringTip);
        AddPhotoInfoRow(
            UiStrings.PhotoInfoExposureMode,
            text.ExposureMode,
            UiStrings.PhotoInfoExposureModeTip);
        AddPhotoInfoRow(
            UiStrings.PhotoInfoWhiteBalance,
            text.WhiteBalance,
            UiStrings.PhotoInfoWhiteBalanceTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoFlash, text.Flash, UiStrings.PhotoInfoFlashTip);
        AddPhotoInfoRow(
            UiStrings.PhotoInfoCaptured,
            text.CaptureDateTime,
            UiStrings.PhotoInfoCapturedTip);
        AddPhotoInfoRow(
            UiStrings.PhotoInfoDimensions,
            text.Dimensions,
            UiStrings.PhotoInfoDimensionsTip);
        AddPhotoInfoRow(UiStrings.PhotoInfoFile, text.File, UiStrings.PhotoInfoFileTip);
        _photoInfoFloatingOverlay.ApplyPlacement();
    }

    private void AddPhotoColorProfile(PhotoColorProfile? profile, System.Globalization.CultureInfo culture)
    {
        if (profile is null)
        {
            return;
        }

        var presentation = PhotoColorProfilePresenter.Format(
            profile,
            culture,
            _perceptualColorNameResolver,
            ColorNameDisplayCatalog.ForLocale(_localizer.Locale));
        var heading = new TextBlock
        {
            Text = _localizer[UiStrings.PhotoInfoColors],
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.88,
            Margin = new Thickness(0, 2, 0, 1),
        };
        ToolTip.SetTip(heading, _localizer[UiStrings.PhotoInfoColorsTip]);
        PhotoInfoRows.Children.Add(heading);

        var dominantContent = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
        };
        dominantContent.Children.Add(CreatePhotoColorSwatch(presentation.Dominant.Color, 32, 26));
        dominantContent.Children.Add(new TextBlock
        {
            Text = presentation.Dominant.StructuralName,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            MaxWidth = 230,
            Opacity = 0.94,
        });
        AddPhotoInfoControlRow(
            UiStrings.PhotoInfoCharacteristic,
            dominantContent,
            FormatPhotoColorTooltip(presentation.Dominant));

        var paletteContent = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
        };
        foreach (var entry in presentation.Palette)
        {
            var item = new StackPanel
            {
                Width = 34,
                Spacing = 2,
            };
            item.Children.Add(CreatePhotoColorSwatch(entry.Color.Color, 30, 24));
            item.Children.Add(new TextBlock
            {
                Text = entry.Share,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                FontSize = 11,
                Opacity = 0.76,
            });
            ToolTip.SetTip(item, FormatPhotoColorTooltip(entry.Color, entry.Share));
            paletteContent.Children.Add(item);
        }

        AddPhotoInfoControlRow(
            UiStrings.PhotoInfoFrequentShades,
            paletteContent,
            _localizer[UiStrings.PhotoInfoColorsTip]);
        if (!presentation.NotableColors.IsEmpty)
        {
            var notableContent = new StackPanel
            {
                Spacing = 5,
            };
            foreach (var colors in PhotoColorProfileLayout.ArrangeNotableColors(
                         presentation.NotableColors))
            {
                var row = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 6,
                };
                foreach (var color in colors)
                {
                    var swatch = CreatePhotoColorSwatch(color.Color, 28, 28);
                    ToolTip.SetTip(swatch, FormatPhotoColorTooltip(color));
                    row.Children.Add(swatch);
                }

                notableContent.Children.Add(row);
            }

            AddPhotoInfoControlRow(
                UiStrings.PhotoInfoNotableColors,
                notableContent,
                _localizer[UiStrings.PhotoInfoColorsTip]);
        }

        PhotoInfoRows.Children.Add(new Separator { Margin = new Thickness(0, 2, 0, 1) });
    }

    private Border CreatePhotoColorSwatch(StageColor color, double width, double height)
    {
        var relativeLuminance = ((0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue)) / 255;
        var outline = relativeLuminance >= 0.58
            ? Color.FromArgb(170, 0, 0, 0)
            : Color.FromArgb(190, 255, 255, 255);
        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(outline),
            Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)),
        };
    }

    private string FormatPhotoColorTooltip(PhotoColorProfileDisplayColor color, string? share = null)
    {
        var lines = new List<string>
        {
            color.StructuralName,
            $"{color.Hex} · {color.Oklch}",
            $"{_localizer[UiStrings.ColorPickerDetailCreativeName]}: {color.CreativeName}",
        };
        if (share is not null)
        {
            lines.Insert(1, $"{_localizer[UiStrings.PhotoInfoShare]}: {share}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void AddPhotoInfoControlRow(string labelKey, Control value, string tooltip)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("128,*"),
            ColumnSpacing = 10,
        };
        row.Children.Add(new TextBlock
        {
            Text = _localizer[labelKey],
            Opacity = 0.68,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        });
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        ToolTip.SetTip(row, tooltip);
        PhotoInfoRows.Children.Add(row);
    }

    private void AddPhotoInfoRow(string labelKey, string? value, string tooltipKey)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var valueText = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.92,
        };
        AddPhotoInfoControlRow(labelKey, valueText, _localizer[tooltipKey]);
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
        ColorPickerHexLabel.Text = _localizer[UiStrings.ColorPickerDetailHex];
        ColorPickerRgbLabel.Text = _localizer[UiStrings.ColorPickerDetailRgb];
        ColorPickerOklchLabel.Text = _localizer[UiStrings.ColorPickerDetailOklch];
        ColorPickerLightnessLabel.Text = _localizer[UiStrings.ColorPickerDetailLightness];
        ColorPickerChromaLabel.Text = _localizer[UiStrings.ColorPickerDetailChroma];
        ColorPickerCreativeNameLabel.Text = _localizer[UiStrings.ColorPickerDetailCreativeName];
        ColorPickerCloseButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        ColorPickerClearButton.Content = FoviumIconCatalog.Create(FoviumIcon.Clear, 14);
        ToolTip.SetTip(ColorPickerDragHandle, _localizer[UiStrings.PresentationMovePanel]);
        ToolTip.SetTip(ColorPickerCloseButton, _localizer[UiStrings.ColorPickerClose]);
        ToolTip.SetTip(ColorPickerClearButton, _localizer[UiStrings.ColorPickerClear]);
        ColorPickerCloseButton.Click += (_, _) =>
        {
            _colorPicker.SetVisible(false);
            PhotoViewport.Focus();
        };
        ColorPickerClearButton.Click += (_, _) =>
        {
            _colorPicker.ClearHistory();
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
        var selectedEntry = _colorPicker.SelectedEntry;
        ColorPickerEmptyText.IsVisible = selectedEntry is null;
        ColorPickerSampleContent.IsVisible = selectedEntry is not null;
        ColorPickerClearButton.IsEnabled = _colorPicker.History.Count > 0;
        ColorPickerHistoryRows.Children.Clear();
        foreach (var historyEntry in _colorPicker.History)
        {
            ColorPickerHistoryRows.Children.Add(CreateColorHistoryRow(
                historyEntry,
                ReferenceEquals(historyEntry, selectedEntry)));
        }

        if (selectedEntry is not null)
        {
            ApplySelectedColorEntry(selectedEntry);
        }

        _colorPickerFloatingOverlay.ApplyPlacement();
        Dispatcher.UIThread.Post(
            _colorPickerFloatingOverlay.ApplyPlacement,
            DispatcherPriority.Loaded);
    }

    private void ApplySelectedColorEntry(ColorHistoryEntry entry)
    {
        var sample = entry.Sample;
        var description = entry.Description;
        ColorPickerMainSwatch.Background = CreateSampleBrush(sample);
        ColorPickerDetailedName.Text = _perceptualColorNameResolver.ResolveDetailed(description);
        ColorPickerHexValue.Text = FormatSampleCode(sample);
        ColorPickerRgbLabel.Text = _localizer[sample.Alpha == byte.MaxValue
            ? UiStrings.ColorPickerDetailRgb
            : UiStrings.ColorPickerDetailRgba];
        ColorPickerRgbValue.Text = FormatSampleComponentValues(sample);
        ColorPickerCreativeNameValue.Text = GetSampleName(sample);
        SetAccuracyToolTip(ColorPickerHexValue, sample);

        var hasPerceptualDetail = !description.IsTransparent;
        SetPerceptualDetailVisibility(hasPerceptualDetail);
        if (!hasPerceptualDetail)
        {
            return;
        }

        ColorPickerOklchValue.Text = PerceptualColorNameResolver.FormatOklch(description.Oklch!.Value);
        ColorPickerHueLabel.Text = _perceptualColorNameResolver.ResolveDetailToneLabel(description);
        ColorPickerHueValue.Text = _perceptualColorNameResolver.ResolveDetailTone(description);
        ColorPickerLightnessValue.Text =
            _perceptualColorNameResolver.ResolveLightness(description.LightnessClass!.Value);
        ColorPickerChromaValue.Text =
            _perceptualColorNameResolver.ResolveChroma(description.ChromaClass!.Value);
    }

    private void SetPerceptualDetailVisibility(bool visible)
    {
        ColorPickerOklchLabel.IsVisible = visible;
        ColorPickerOklchValue.IsVisible = visible;
        ColorPickerHueLabel.IsVisible = visible;
        ColorPickerHueValue.IsVisible = visible;
        ColorPickerLightnessLabel.IsVisible = visible;
        ColorPickerLightnessValue.IsVisible = visible;
        ColorPickerChromaLabel.IsVisible = visible;
        ColorPickerChromaValue.IsVisible = visible;
        ColorPickerCreativeNameLabel.IsVisible = visible;
        ColorPickerCreativeNameValue.IsVisible = visible;
    }

    private Control CreateColorHistoryRow(ColorHistoryEntry entry, bool isSelected)
    {
        var sample = entry.Sample;
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
        var shortName = _perceptualColorNameResolver.ResolveShort(entry.Description);
        var nameText = new TextBlock
        {
            Text = shortName,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        ToolTip.SetTip(nameText, shortName);
        row.Children.Add(nameText);

        var button = new Button
        {
            Content = row,
            Focusable = false,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Padding = new Thickness(6, 5),
            BorderThickness = new Thickness(1),
            BorderBrush = isSelected
                ? new SolidColorBrush(Color.FromArgb(0x58, 0x78, 0xA8, 0xD8))
                : Brushes.Transparent,
            Background = isSelected
                ? new SolidColorBrush(Color.FromArgb(0x28, 0x78, 0xA8, 0xD8))
                : Brushes.Transparent,
        };
        button.Click += (_, _) =>
        {
            _colorPicker.Select(entry.EntryId);
            PhotoViewport.Focus();
        };
        return button;
    }

    private static IBrush CreateSampleBrush(ColorSample sample) => new SolidColorBrush(
        Color.FromArgb(sample.Alpha, sample.Red, sample.Green, sample.Blue));

    private string GetSampleName(ColorSample sample) => _colorSampleNameResolver.Resolve(sample);

    private static string FormatSampleCode(ColorSample sample) =>
        sample.Accuracy == ColorSampleAccuracy.Approximate ? $"≈ {sample.Hex}" : sample.Hex;

    private static string FormatSampleComponentValues(ColorSample sample) => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        sample.Alpha == byte.MaxValue ? "{0}, {1}, {2}" : "{0}, {1}, {2}, {3}",
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