using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Fovium.Diagnostics;
using Fovium.ColorManagement;
using Fovium.ColorPicking;
using Fovium.Imaging;
using Fovium.Input;
using Fovium.Loading;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Slideshow;
using Fovium.Stage;

namespace Fovium.Viewer;

internal readonly record struct ViewportAmbientPresentationState(
    long? ImageIdentity,
    long? AmbientIdentity,
    StageBackgroundMode BackgroundMode,
    bool HasMatchingAmbient);

internal readonly record struct AtomicPhotoPresentationState(
    long? PresentedNumericIdentity,
    string? PresentedIdentity,
    Fovium.Rendering.PixelSize? PresentedOrientedSize,
    RectD? PresentedDestination,
    StageSettings? PresentedStage,
    long? PresentedAmbientIdentity,
    long? PendingNumericIdentity,
    string? PendingIdentity,
    Fovium.Rendering.PixelSize? PendingOrientedSize,
    StageSettings? PendingStage,
    bool PhotoPresentationVisible,
    bool HasManagedSource);

internal sealed class PendingPhotoPresentation : IDisposable
{
    private SharedResourceLease<DecodedImage>? _image;
    private DecodedImage.AmbientLease? _ambient;

    public PendingPhotoPresentation(
        SharedResourceLease<DecodedImage> image,
        ViewTransfer transfer,
        string identity,
        StageSettings stage,
        DecodedImage.AmbientLease? ambient,
        long? ambientIdentity)
    {
        _image = image;
        Transfer = transfer;
        Identity = identity;
        Stage = stage;
        _ambient = ambient;
        AmbientIdentity = ambientIdentity;
        RequestedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public SharedResourceLease<DecodedImage> Image => _image
        ?? throw new ObjectDisposedException(nameof(PendingPhotoPresentation));

    public long NumericIdentity => Image.Value.Identity;

    public ViewTransfer Transfer { get; }

    public string Identity { get; }

    public StageSettings Stage { get; private set; }

    public long? AmbientIdentity { get; private set; }

    public long RequestedTimestamp { get; }

    public ManagedPhotoKey? ManagedKey { get; set; }

    public void UpdateStage(StageSettings stage, DecodedImage.AmbientLease? ambient, long? ambientIdentity)
    {
        var previous = _ambient;
        Stage = stage;
        _ambient = ambient;
        AmbientIdentity = ambientIdentity;
        previous?.Dispose();
    }

    public SharedResourceLease<DecodedImage> TakeImage() => Interlocked.Exchange(ref _image, null)
        ?? throw new ObjectDisposedException(nameof(PendingPhotoPresentation));

    public DecodedImage.AmbientLease? TakeAmbient() => Interlocked.Exchange(ref _ambient, null);

    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Interlocked.Exchange(ref _ambient, null)?.Dispose();
    }
}

internal sealed class PhotoViewportControl : Control, IPresentedImageSource
{
    private readonly ViewportModel _viewport = new();
    private readonly AmbientRenderFrameDiagnostics _ambientFrameDiagnostics = new();
    private readonly CompositionCustomDrawHost _photoDrawHost = new();
    private SharedResourceLease<DecodedImage>? _image;
    private DecodedImage.AmbientLease? _ambient;
    private long? _ambientImageIdentity;
    private SharedResourceLease<DecodedImage>? _inspectionImage;
    private DecodedImage.AmbientLease? _inspectionAmbient;
    private long? _inspectionAmbientImageIdentity;
    private StageSettings? _inspectionStage;
    private string? _inspectionImageIdentity;
    private ViewTransfer? _inspectionRestore;
    private InspectionMode _inspectionMode;
    private Point? _lastDragPoint;
    private IPointer? _drawingPointer;
    private PointD? _lastPointerPosition;
    private bool _pointerInside;
    private TopLevel? _topLevel;
    private double _wheelAccumulator;
    private StageSettings _stage = StageSettings.Default;
    private PresentationOverlaySession? _presentation;
    private MarkupOverlayControl? _markupOverlay;
    private PointerFeedbackOverlayControl? _pointerFeedbackOverlay;
    private string? _canonicalImageIdentity;
    private Cursor? _visibleViewerCursor;
    private Cursor? _hiddenViewerCursor;
    private Cursor? _handViewerCursor;
    private ViewerSystemCursorMode _appliedSystemCursorMode = (ViewerSystemCursorMode)(-1);
    private InteractionRenderDiagnostics _interactionDiagnostics = new();
    private bool _colorPickerEnabled;
    private ManagedPhotoPresentationCoordinator? _managedPhotoCoordinator;
    private ManagedPhotoSourceLease? _managedSource;
    private PendingPhotoPresentation? _pendingPresentation;
    private DisplayProfileResolution _displayProfile = new(
        MonitorColorState.DestinationUnavailable,
        null,
        "No display profile has been resolved.");
    private bool _displayProfileResolved;
    private ManagedPhotoKey? _requestedManagedKey;
    private bool _monitorColorManagementEnabled = true;
    private bool _monitorColorEngineAvailable;
    private bool _monitorColorPlatformSupported = OperatingSystem.IsWindows();
    private bool _managedPresentationFailed;
    private MonitorColorState _monitorColorState = MonitorColorState.PlatformUnsupported;
    private bool _photoPresentationVisible;
    private readonly PhotoPresentationViewSession _photoPresentationView = new();
    private PhotoPresentationViewSettings _photoPresentationViewSettings =
        PhotoPresentationViewSettings.Default;

    public PhotoViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;
        FocusAdorner = null;
        CacheMode = new BitmapCache { SnapsToDevicePixels = true };
        _photoPresentationView.Changed += OnPhotoPresentationViewChanged;
    }

    public event EventHandler? PointerActivity;

    public event EventHandler? ViewStateChanged;

    public event EventHandler? PresentedImageChanged;

    public event EventHandler<PhotoSampleRequestedEventArgs>? ColorSampleRequested;

    public event EventHandler? MonitorColorStateChanged;

    public bool HasImage => _image is not null;

    public InspectionMode InspectionMode => _inspectionMode;

    internal bool PhotoPresentationViewEnabled => _photoPresentationView.IsEnabled;

    internal PhotoPresentationViewSession PhotoPresentationView => _photoPresentationView;

    internal MonitorColorState MonitorColorState => _monitorColorState;

    internal ManagedPhotoCoordinatorMetrics? MonitorColorMetrics => _managedPhotoCoordinator?.Metrics;

    internal long CurrentManagedSourceBytes => _managedSource?.Source.RetainedBytes ?? 0;

    internal Task WaitForManagedPhotoIdleAsync() =>
        _managedPhotoCoordinator?.WaitForIdleAsync() ?? Task.CompletedTask;

    internal async Task<SlideshowPreparationResult> PrepareSlideshowNextAsync(
        SharedResourceLease<DecodedImage> image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var descriptor = image.Value.Descriptor;
        if (ClassifyMonitorState(descriptor) != MonitorColorState.Managed ||
            _displayProfile.Profile is not { } profile ||
            _managedPhotoCoordinator is not { } coordinator)
        {
            return new SlideshowPreparationResult(
                SlideshowPreparationStatus.NotRequired,
                PreparationDuration: stopwatch.Elapsed);
        }

        var key = CreateManagedKey(image.Value, profile.Identity);
        if (coordinator.TryAcquire(key, out var existing) && existing is not null)
        {
            using (existing)
            {
                return new SlideshowPreparationResult(
                    SlideshowPreparationStatus.Ready,
                    existing.Source.RetainedBytes,
                    stopwatch.Elapsed);
            }
        }

        var nextBytes = checked((long)descriptor.EncodedSize.Width * descriptor.EncodedSize.Height * 4);
        var currentBytes = _managedSource?.Source.RetainedBytes ?? 0;
        if (!SlideshowManagedPreloadPolicy.IsAdmitted(currentBytes, nextBytes))
        {
            return new SlideshowPreparationResult(
                SlideshowPreparationStatus.RejectedByMemory,
                nextBytes,
                stopwatch.Elapsed);
        }

        coordinator.Request(new ManagedPhotoRenderRequest(
            key,
            descriptor,
            image.Value.AcquireRenderLease(),
            profile.Bytes));
        await coordinator.WaitForIdleAsync().WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_displayProfile.Profile?.Identity != key.DestinationIdentity ||
            !coordinator.TryAcquire(key, out var prepared) ||
            prepared is null)
        {
            return new SlideshowPreparationResult(
                SlideshowPreparationStatus.Stale,
                nextBytes,
                stopwatch.Elapsed);
        }

        using (prepared)
        {
            return new SlideshowPreparationResult(
                SlideshowPreparationStatus.Ready,
                prepared.Source.RetainedBytes,
                stopwatch.Elapsed);
        }
    }

    internal AtomicPhotoPresentationState CaptureAtomicPresentationState() => new(
        _image?.Value.Identity,
        _canonicalImageIdentity,
        _image?.Value.Descriptor.OrientedSize,
        _image is null ? null : GetDestination(),
        _image is null ? null : _stage,
        _ambientImageIdentity,
        _pendingPresentation?.NumericIdentity,
        _pendingPresentation?.Identity,
        _pendingPresentation?.Image.Value.Descriptor.OrientedSize,
        _pendingPresentation?.Stage,
        _photoPresentationVisible,
        _managedSource is not null);

    internal string? PresentedImageIdentity => _inspectionImage is not null
        ? _inspectionImageIdentity
        : _canonicalImageIdentity;

    public bool TryAcquirePresentedImage(out PresentedImageLease? image)
    {
        var lease = _inspectionImage ?? _image;
        var identity = PresentedImageIdentity;
        if (lease is null || identity is null)
        {
            image = null;
            return false;
        }

        image = new PresentedImageLease(lease.Acquire(), identity);
        return true;
    }

    public void SetColorPickerEnabled(bool enabled)
    {
        if (_colorPickerEnabled == enabled)
        {
            return;
        }

        _colorPickerEnabled = enabled;
        UpdatePointerPresentation();
        ApplyCursor();
    }

    internal MarkupRenderSnapshot CapturePresentedMarkup() =>
        _presentation?.GetRenderSnapshot(PresentedImageIdentity) ?? MarkupRenderSnapshot.Empty;

    public ViewTransfer CaptureViewTransfer() =>
        _image is null || _photoPresentationView.IsEnabled
            ? ViewTransfer.Fit
            : _viewport.CaptureTransfer();

    internal PhotoPresentationLayoutResult? CapturePhotoPresentationLayout()
    {
        if (!_photoPresentationView.IsEnabled || GetPresentedImageLease() is not { } image)
        {
            return null;
        }

        return CalculatePhotoPresentationLayout(image.Value.Descriptor.OrientedSize);
    }

    internal void SetPhotoPresentationViewEnabled(bool enabled) =>
        _photoPresentationView.SetEnabled(enabled);

    private void OnPhotoPresentationViewChanged(object? sender, EventArgs e)
    {
        _lastDragPoint = null;
        _wheelAccumulator = 0;
        if (_photoPresentationView.IsEnabled)
        {
            _presentation?.EndTemporaryHand();
        }

        if (!_photoPresentationView.IsEnabled && _image is not null)
        {
            _viewport.Fit();
        }

        InvalidateAndReport();
    }

    internal void SetPhotoPresentationViewSettings(PhotoPresentationViewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        if (_photoPresentationViewSettings == normalized)
        {
            return;
        }

        _photoPresentationViewSettings = normalized;
        if (_photoPresentationView.IsEnabled)
        {
            InvalidateAndReport();
        }
    }

    internal AmbientRenderFrameMetrics GetAmbientRenderFrameMetrics() =>
        _ambientFrameDiagnostics.GetMetrics();

    internal void EnableAmbientPipelineDiagnostics() =>
        _ambientFrameDiagnostics.EnablePipelineTracking();

    internal void ConfigureInteractionDiagnostics(InteractionRenderDiagnostics diagnostics) =>
        _interactionDiagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

    internal void ConfigureMonitorColorManagement(IColorTransformEngine engine, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (_managedPhotoCoordinator is not null)
        {
            throw new InvalidOperationException("Monitor color management is already configured.");
        }

        _monitorColorManagementEnabled = enabled;
        _monitorColorEngineAvailable = engine.IsAvailable;
        _managedPhotoCoordinator = new ManagedPhotoPresentationCoordinator(
            new SkiaLittleCmsPhotoRenderer(engine));
        _managedPhotoCoordinator.PresentationChanged += OnManagedPresentationChanged;
        _managedPhotoCoordinator.PresentationFailed += OnManagedPresentationFailed;
        PrepareCurrentManagedSource();
    }

    internal void ConfigureMonitorColorManagement(
        IManagedPhotoRenderer renderer,
        bool enabled,
        bool engineAvailable,
        bool platformSupported)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (_managedPhotoCoordinator is not null)
        {
            throw new InvalidOperationException("Monitor color management is already configured.");
        }

        _monitorColorManagementEnabled = enabled;
        _monitorColorEngineAvailable = engineAvailable;
        _monitorColorPlatformSupported = platformSupported;
        _managedPhotoCoordinator = new ManagedPhotoPresentationCoordinator(renderer);
        _managedPhotoCoordinator.PresentationChanged += OnManagedPresentationChanged;
        _managedPhotoCoordinator.PresentationFailed += OnManagedPresentationFailed;
        PrepareCurrentManagedSource();
    }

    internal void SetMonitorColorManagementEnabled(bool enabled)
    {
        if (_monitorColorManagementEnabled == enabled)
        {
            return;
        }

        _monitorColorManagementEnabled = enabled;
        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        _managedPhotoCoordinator?.Clear();
        DisposeManagedSource();
        if (!enabled && _pendingPresentation is not null)
        {
            CommitPendingPresentation(null, false);
        }
        else if (_pendingPresentation is not null)
        {
            PreparePendingPresentation();
        }
        else
        {
            PrepareCurrentManagedSource();
        }
    }

    internal bool SetDisplayProfile(DisplayProfileResolution profile)
    {
        var wasResolved = _displayProfileResolved;
        _displayProfileResolved = true;
        var unchanged = _displayProfile.State == profile.State &&
            _displayProfile.AdvancedColorEnabled == profile.AdvancedColorEnabled &&
            _displayProfile.Profile?.Identity == profile.Profile?.Identity;
        _displayProfile = profile;
        if (wasResolved && unchanged)
        {
            return false;
        }

        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        _managedPhotoCoordinator?.Clear();
        DisposeManagedSource();
        if (_pendingPresentation is not null)
        {
            PreparePendingPresentation();
        }
        else
        {
            PrepareCurrentManagedSource();
        }

        return true;
    }

    internal void ShutdownMonitorColorManagement()
    {
        var coordinator = _managedPhotoCoordinator;
        if (coordinator is null)
        {
            return;
        }

        coordinator.PresentationChanged -= OnManagedPresentationChanged;
        coordinator.PresentationFailed -= OnManagedPresentationFailed;
        _managedPhotoCoordinator = null;
        _requestedManagedKey = null;
        DisposeManagedSource();
        DisposePendingPresentation();
        coordinator.Dispose();
    }

    internal ViewportAmbientPresentationState CaptureAmbientPresentationState()
    {
        var imageIdentity = _image?.Value.Identity;
        return new ViewportAmbientPresentationState(
            imageIdentity,
            _ambientImageIdentity,
            _stage.BackgroundMode,
            imageIdentity is not null && imageIdentity == _ambientImageIdentity && _ambient is not null);
    }

    public void ConfigurePresentation(
        PresentationOverlaySession presentation,
        Cursor? visibleViewerCursor = null,
        Cursor? hiddenViewerCursor = null,
        Cursor? handViewerCursor = null,
        MarkupOverlayControl? markupOverlay = null,
        PointerFeedbackOverlayControl? pointerFeedbackOverlay = null)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (_presentation is not null)
        {
            _presentation.Changed -= OnPresentationChanged;
        }

        _presentation = presentation;
        _visibleViewerCursor = visibleViewerCursor;
        _hiddenViewerCursor = hiddenViewerCursor;
        _handViewerCursor = handViewerCursor;
        _markupOverlay = markupOverlay;
        _pointerFeedbackOverlay = pointerFeedbackOverlay;
        _appliedSystemCursorMode = (ViewerSystemCursorMode)(-1);
        presentation.Changed += OnPresentationChanged;
        UpdateMarkupOverlay();
        UpdatePointerPresentation();
        ApplyCursor();
    }

    public void SetViewerCursor(Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        if (ReferenceEquals(_visibleViewerCursor, cursor))
        {
            return;
        }

        _visibleViewerCursor = cursor;
        _appliedSystemCursorMode = (ViewerSystemCursorMode)(-1);
        ApplyCursor();
    }

    public void SetImage(
        SharedResourceLease<DecodedImage> image,
        ViewTransfer transfer,
        string imageIdentity)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageIdentity);
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
        _ambient = null;
        _ambientImageIdentity = null;
        _image = image;
        _canonicalImageIdentity = imageIdentity;
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, transfer);
        ResetManagedPhotoRequest();
        _presentation?.SelectImage(imageIdentity);
        UpdateMarkupOverlay();
        PrepareCurrentManagedSource();
        PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        previous?.Dispose();
        previousAmbient?.Dispose();
        RaiseViewStateChanged();
    }

    public void SetPresentation(
        SharedResourceLease<DecodedImage> image,
        ViewTransfer transfer,
        string imageIdentity,
        StagePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageIdentity);
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.ImageIdentity != image.Value.Identity)
        {
            throw new InvalidOperationException("Stage presentation identity does not match the photograph.");
        }

        var ambient = TakeMatchingAmbient(presentation);
        DiscardInspection();

        if (_image?.Value.Identity == image.Value.Identity &&
            string.Equals(_canonicalImageIdentity, imageIdentity, StringComparison.Ordinal))
        {
            DisposePendingPresentation();
            _managedPhotoCoordinator?.Clear();
            _requestedManagedKey = _managedSource?.Source.Key;
            ReplaceCurrentStage(presentation.Stage, ambient, presentation.ImageIdentity);
            image.Dispose();
            PublishPhotoPresentation();
            return;
        }

        var pending = new PendingPhotoPresentation(
            image,
            transfer,
            imageIdentity,
            presentation.Stage,
            ambient,
            ambient is null ? null : presentation.ImageIdentity);
        var previousPending = _pendingPresentation;
        _pendingPresentation = pending;
        previousPending?.Dispose();
        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        PreparePendingPresentation();
    }

    public void SetStage(StagePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var ambient = TakeMatchingAmbient(presentation);
        if (_pendingPresentation is { } pending && presentation.ImageIdentity == pending.NumericIdentity)
        {
            pending.UpdateStage(
                presentation.Stage,
                ambient,
                ambient is null ? null : presentation.ImageIdentity);
            return;
        }

        if (_image?.Value.Identity == presentation.ImageIdentity ||
            (_image is null && _pendingPresentation is null && presentation.ImageIdentity is null))
        {
            ReplaceCurrentStage(
                presentation.Stage,
                ambient,
                ambient is null ? null : presentation.ImageIdentity);
            PublishPhotoPresentation();
            return;
        }

        ambient?.Dispose();
    }

    public void ClearImage()
    {
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
        DisposePendingPresentation();
        DisposeManagedSource();
        _image = null;
        _canonicalImageIdentity = null;
        ResetManagedPhotoRequest();
        _presentation?.SelectImage(null);
        _ambient = null;
        _ambientImageIdentity = null;
        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        previous?.Dispose();
        previousAmbient?.Dispose();
    }

    public void Fit()
    {
        if (_image is null || _photoPresentationView.IsEnabled)
        {
            return;
        }

        _viewport.Fit();
        InvalidateAndReport();
    }

    public void SetPhotographic100AtCenter()
    {
        if (_image is null || _photoPresentationView.IsEnabled)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomAt(center, 1);
        InvalidateAndReport();
    }

    public void ZoomByStepsAtCenter(int steps)
    {
        if (_image is null || _photoPresentationView.IsEnabled || steps == 0)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomBySteps(center, steps);
        InvalidateAndReport();
    }

    public bool BeginPeek100()
    {
        if (_image is null ||
            !PhotoPresentationInputPolicy.Allows(
                PhotoPresentationInteraction.Peek,
                _photoPresentationView.IsEnabled) ||
            _inspectionMode != InspectionMode.None)
        {
            return false;
        }

        _inspectionRestore = _viewport.CaptureTransfer();
        _inspectionMode = InspectionMode.Peek100;
        _lastDragPoint = null;
        _viewport.SetPhotographic100ForInspection(_lastPointerPosition);
        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        return true;
    }

    public bool BeginBlinkCompare()
    {
        if (_image is null ||
            !PhotoPresentationInputPolicy.Allows(
                PhotoPresentationInteraction.Blink,
                _photoPresentationView.IsEnabled) ||
            _inspectionMode != InspectionMode.None)
        {
            return false;
        }

        _inspectionRestore = _viewport.CaptureTransfer();
        _inspectionMode = InspectionMode.BlinkCompare;
        _lastDragPoint = null;
        return true;
    }

    public bool ShowBlinkComparison(
        SharedResourceLease<DecodedImage> image,
        string imageIdentity,
        StageSettings stage,
        DecodedImage.AmbientLease? ambient)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageIdentity);
        ArgumentNullException.ThrowIfNull(stage);
        if (_inspectionMode != InspectionMode.BlinkCompare || _inspectionRestore is null)
        {
            ambient?.Dispose();
            return false;
        }

        var previousImage = _inspectionImage;
        var previousAmbient = _inspectionAmbient;
        _inspectionImage = image;
        _inspectionImageIdentity = imageIdentity;
        _inspectionAmbient = ambient;
        _inspectionAmbientImageIdentity = ambient is null ? null : image.Value.Identity;
        _inspectionStage = stage;
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, _inspectionRestore.Value);
        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        previousImage?.Dispose();
        previousAmbient?.Dispose();
        return true;
    }

    public bool EndInspection()
    {
        if (_inspectionMode == InspectionMode.None)
        {
            return false;
        }

        var restore = _inspectionRestore;
        var comparison = _inspectionImage;
        var comparisonAmbient = _inspectionAmbient;
        _inspectionMode = InspectionMode.None;
        _inspectionRestore = null;
        _inspectionImage = null;
        _inspectionImageIdentity = null;
        _inspectionAmbient = null;
        _inspectionAmbientImageIdentity = null;
        _inspectionStage = null;
        _lastDragPoint = null;
        if (_image is not null && restore is not null)
        {
            _viewport.SetImage(_image.Value.Descriptor.OrientedSize, restore.Value);
        }

        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        if (comparison is not null)
        {
            PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        }

        comparison?.Dispose();
        comparisonAmbient?.Dispose();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
        }

        _photoDrawHost.Attach(this, Bounds.Size);
        UpdateViewportMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        _photoDrawHost.Detach(this);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _photoDrawHost.Resize(e.NewSize);
        UpdateViewportMetrics();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        if (_inspectionMode != InspectionMode.None)
        {
            e.Handled = true;
            return;
        }
        if (!PhotoPresentationInputPolicy.Allows(
                PhotoPresentationInteraction.WheelZoom,
                _photoPresentationView.IsEnabled))
        {
            _wheelAccumulator = 0;
            e.Handled = true;
            return;
        }
        if (_image is null)
        {
            return;
        }

        _wheelAccumulator += e.Delta.Y;
        var steps = (int)Math.Truncate(_wheelAccumulator);
        if (steps == 0)
        {
            return;
        }

        _wheelAccumulator -= steps;
        var pointer = e.GetPosition(this);
        _viewport.ZoomBySteps(new PointD(pointer.X, pointer.Y), steps);
        InvalidateAndReport();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _image is null)
        {
            return;
        }

        var colorPickerAction = ColorPickerInteraction.ResolvePrimaryClick(
            _colorPickerEnabled,
            _presentation?.TemporaryHandActive == true);
        if (colorPickerAction == ColorPickerPrimaryClickAction.Pan &&
            _inspectionMode == InspectionMode.None)
        {
            if (!PhotoPresentationInputPolicy.Allows(
                    PhotoPresentationInteraction.HandPan,
                    _photoPresentationView.IsEnabled))
            {
                e.Handled = true;
                return;
            }

            _lastDragPoint = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (colorPickerAction == ColorPickerPrimaryClickAction.Sample)
        {
            if (PhotoPresentationInputPolicy.Allows(
                    PhotoPresentationInteraction.ColorSampling,
                    _photoPresentationView.IsEnabled))
            {
                RequestColorSample(e.GetPosition(this));
            }

            e.Handled = true;
            return;
        }

        if (_inspectionMode != InspectionMode.None)
        {
            if (_inspectionMode == InspectionMode.Peek100 && e.ClickCount != 2)
            {
                _lastDragPoint = e.GetPosition(this);
                e.Pointer.Capture(this);
            }

            e.Handled = true;
            return;
        }

        if (_presentation is { MarkupToolsVisible: true } presentation)
        {
            var pointer = e.GetPosition(this);
            if (MarkupPointerInteraction.ForTool(presentation.EffectiveTool) ==
                MarkupPointerGesture.Pan)
            {
                if (!PhotoPresentationInputPolicy.Allows(
                        PhotoPresentationInteraction.HandPan,
                        _photoPresentationView.IsEnabled))
                {
                    e.Handled = true;
                    return;
                }

                _lastDragPoint = pointer;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            var source = TryGetSourcePoint(pointer);
            if (PhotoPresentationInputPolicy.Allows(
                    PhotoPresentationInteraction.MarkupDrawing,
                    _photoPresentationView.IsEnabled) &&
                source is not null && presentation.BeginDrawing(
                    source.Value,
                    GetPresentedPhysicalScale(),
                    GetPresentedOrientedSize()))
            {
                _drawingPointer = e.Pointer;
                e.Pointer.Capture(this);
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            if (!PhotoPresentationInputPolicy.Allows(
                    PhotoPresentationInteraction.DoubleClickZoom,
                    _photoPresentationView.IsEnabled))
            {
                e.Handled = true;
                return;
            }

            var pointer = e.GetPosition(this);
            _viewport.ToggleFitAnd100(new PointD(pointer.X, pointer.Y));
            _lastDragPoint = null;
            InvalidateAndReport();
            e.Handled = true;
            return;
        }

        if (PhotoPresentationInputPolicy.Allows(
                PhotoPresentationInteraction.DragPan,
                _photoPresentationView.IsEnabled))
        {
            _lastDragPoint = e.GetPosition(this);
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _interactionDiagnostics.RecordPointerMoved();
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        if (_drawingPointer == e.Pointer &&
            _presentation is { IsDrawing: true } presentation &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var pointer = e.GetPosition(this);
            presentation.ContinueDrawing(
                GetClampedSourcePoint(pointer),
                ToDrawingModifiers(e.KeyModifiers));
            e.Handled = true;
            return;
        }

        var previous = _lastDragPoint;
        if (previous is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!PhotoPresentationInputPolicy.Allows(
                PhotoPresentationInteraction.DragPan,
                _photoPresentationView.IsEnabled))
        {
            _lastDragPoint = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        var current = e.GetPosition(this);
        _viewport.PanBy(new PointD(current.X - previous.Value.X, current.Y - previous.Value.Y));
        _lastDragPoint = current;
        if (_inspectionMode == InspectionMode.Peek100)
        {
            UpdateMarkupOverlay();
            PublishPhotoPresentation();
        }
        else
        {
            InvalidateAndReport();
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        if (_drawingPointer == e.Pointer && _presentation is { IsDrawing: true } presentation)
        {
            presentation.EndDrawing(
                GetClampedSourcePoint(e.GetPosition(this)),
                ToDrawingModifiers(e.KeyModifiers));
            _drawingPointer = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_lastDragPoint is null)
        {
            return;
        }

        _lastDragPoint = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private RectD GetDestination()
    {
        if (_photoPresentationView.IsEnabled && GetPresentedImageLease() is { } image)
        {
            return CalculatePhotoPresentationLayout(image.Value.Descriptor.OrientedSize)
                .PhotoDestination;
        }

        var destination = _viewport.DestinationDip;
        if (!_viewport.UsesExactPixelSampling)
        {
            return destination;
        }

        var aligned = _viewport.PhysicalAlignedOrigin();
        return destination with { X = aligned.X, Y = aligned.Y };
    }

    internal void CancelPendingPresentation()
    {
        if (_pendingPresentation is null)
        {
            return;
        }

        DisposePendingPresentation();
        _managedPhotoCoordinator?.Clear();
        _requestedManagedKey = _managedSource?.Source.Key;
        _managedPresentationFailed = false;
        PublishPhotoPresentation();
    }

    private PhotoPresentationLayoutResult CalculatePhotoPresentationLayout(
        Fovium.Rendering.PixelSize sourceSize) =>
        PhotoPresentationLayout.Calculate(
            _viewport.ViewportSize,
            _viewport.RenderScaling,
            sourceSize,
            GetPresentedStage(),
            _photoPresentationViewSettings.EdgeMarginPercent);

    private SharedResourceLease<DecodedImage>? GetPresentedImageLease() =>
        _inspectionImage ?? _image;

    private StageSettings GetPresentedStage() =>
        _inspectionImage is not null ? _inspectionStage! : _stage;

    private Fovium.Rendering.PixelSize GetPresentedOrientedSize() =>
        GetPresentedImageLease()?.Value.Descriptor.OrientedSize ?? _viewport.SourceSize;

    private double GetPresentedPhysicalScale() => _photoPresentationView.IsEnabled
        ? CalculatePhotoPresentationLayout(GetPresentedOrientedSize()).PhysicalScale
        : _viewport.PhysicalScale;

    private bool UsesExactPixelSampling() => _photoPresentationView.IsEnabled
        ? CalculatePhotoPresentationLayout(GetPresentedOrientedSize()).UsesExactPixelSampling
        : _viewport.UsesExactPixelSampling;

    private void PublishPhotoPresentation()
    {
        var cachedLease = _inspectionImage ?? _image;
        if (cachedLease is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            _photoDrawHost.SetOperation(null);
            SetPhotoPresentationVisible(false);
            return;
        }

        DecodedImage.RenderLease? renderLease = null;
        DecodedImage.AmbientLease? ambientLease = null;
        ManagedPhotoSourceLease? managedSource = null;
        try
        {
            renderLease = cachedLease.Value.AcquireRenderLease();
            var ambient = _inspectionImage is not null ? _inspectionAmbient : _ambient;
            var presentedNumericIdentity = cachedLease.Value.Identity;
            var ambientIdentity = _inspectionImage is not null
                ? _inspectionAmbientImageIdentity
                : _ambientImageIdentity;
            if (ambientIdentity == presentedNumericIdentity)
            {
                ambientLease = ambient?.Acquire();
            }

            var descriptor = cachedLease.Value.Descriptor;
            var presentationStage = _inspectionImage is not null ? _inspectionStage! : _stage;
            var destination = GetDestination();
            var suppressLegacyPhoto = false;
            var photoPresentationVisible = true;
            var state = ClassifyMonitorState(descriptor);
            if (_managedPresentationFailed && state == MonitorColorState.Managed)
            {
                state = MonitorColorState.InvalidDestinationProfile;
            }

            if (state == MonitorColorState.Managed &&
                _displayProfile.Profile is { } profile &&
                _managedPhotoCoordinator is { } coordinator)
            {
                var key = CreateManagedKey(cachedLease.Value, profile.Identity);
                var acquired = _inspectionImage is not null
                    ? coordinator.TryAcquire(key, out managedSource)
                    : TryAcquirePublishedManagedSource(key, out managedSource);
                suppressLegacyPhoto = !acquired;
                photoPresentationVisible = acquired;

                if (_inspectionImage is not null &&
                    ManagedPhotoRequestPolicy.ShouldRequest(_requestedManagedKey, key))
                {
                    _requestedManagedKey = key;
                    coordinator.Request(new ManagedPhotoRenderRequest(
                        key,
                        descriptor,
                        cachedLease.Value.AcquireRenderLease(),
                        profile.Bytes));
                }
            }
            else
            {
                _requestedManagedKey = null;
            }

            SetMonitorColorState(state);
            _ambientFrameDiagnostics.RecordCustomDrawScheduled();
            _photoDrawHost.SetOperation(new SkiaPhotoDrawOperation(
                new Rect(Bounds.Size),
                renderLease,
                descriptor.EncodedSize,
                descriptor.Orientation,
                destination,
                UsesExactPixelSampling(),
                presentationStage,
                _viewport.RenderScaling,
                ambientLease,
                presentedNumericIdentity,
                ambientLease is null ? null : ambientIdentity,
                _ambientFrameDiagnostics,
                _interactionDiagnostics,
                managedSource,
                suppressLegacyPhoto,
                _managedPhotoCoordinator));
            SetPhotoPresentationVisible(photoPresentationVisible);
            renderLease = null;
            ambientLease = null;
            managedSource = null;
        }
        finally
        {
            renderLease?.Dispose();
            ambientLease?.Dispose();
            managedSource?.Dispose();
        }
    }

    private void ResetManagedPhotoRequest()
    {
        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        DisposeManagedSource();
        DisposePendingPresentation();
        _managedPhotoCoordinator?.Clear();
    }

    private void OnManagedPresentationChanged(object? sender, ManagedPhotoPresentationEventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ProcessManagedPresentationAvailability(e.Key));

    private void OnManagedPresentationFailed(object? sender, ManagedPhotoPresentationEventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ProcessManagedPresentationFailure(e.Key));

    internal void ProcessManagedPresentationAvailability() =>
        ProcessManagedPresentationAvailability(null);

    private void ProcessManagedPresentationAvailability(ManagedPhotoKey? completedKey)
    {
        if (_managedPhotoCoordinator is not { } coordinator)
        {
            return;
        }

        if (_pendingPresentation is { ManagedKey: { } pendingKey } &&
            (completedKey is null || completedKey == pendingKey) &&
            coordinator.TryAcquire(pendingKey, out var pendingSource) &&
            pendingSource is not null)
        {
            CommitPendingPresentation(pendingSource, false);
            return;
        }

        if (_inspectionImage is not null)
        {
            PublishPhotoPresentation();
            return;
        }

        if (_image is not null && TryCreateManagedKey(_image.Value, out var currentKey) &&
            coordinator.TryAcquire(currentKey, out var currentSource) && currentSource is not null)
        {
            var previous = _managedSource;
            _managedSource = currentSource;
            _requestedManagedKey = currentKey;
            _managedPresentationFailed = false;
            PublishPhotoPresentation();
            previous?.Dispose();
        }
    }

    internal void ProcessManagedPresentationFailure() =>
        ProcessManagedPresentationFailure(_requestedManagedKey);

    private void ProcessManagedPresentationFailure(ManagedPhotoKey? failedKey)
    {
        if (failedKey is null)
        {
            return;
        }

        if (_pendingPresentation is { } pending && pending.ManagedKey == failedKey)
        {
            CommitPendingPresentation(null, true);
            return;
        }

        if (_image is not null && TryCreateManagedKey(_image.Value, out var currentKey) &&
            currentKey == failedKey)
        {
            _managedPresentationFailed = true;
            PublishPhotoPresentation();
        }
    }

    private void PreparePendingPresentation()
    {
        var pending = _pendingPresentation;
        if (pending is null)
        {
            return;
        }

        if (!_displayProfileResolved && _monitorColorManagementEnabled &&
            _monitorColorPlatformSupported && _monitorColorEngineAvailable)
        {
            return;
        }

        var state = ClassifyMonitorState(pending.Image.Value.Descriptor);
        SetMonitorColorState(state);
        if (state != MonitorColorState.Managed ||
            _displayProfile.Profile is not { } profile ||
            _managedPhotoCoordinator is not { } coordinator)
        {
            CommitPendingPresentation(null, false);
            return;
        }

        var key = CreateManagedKey(pending.Image.Value, profile.Identity);
        pending.ManagedKey = key;
        if (coordinator.TryAcquire(key, out var ready) && ready is not null)
        {
            CommitPendingPresentation(ready, false);
            return;
        }

        if (!ManagedPhotoRequestPolicy.ShouldRequest(_requestedManagedKey, key))
        {
            return;
        }

        _requestedManagedKey = key;
        coordinator.Request(new ManagedPhotoRenderRequest(
            key,
            pending.Image.Value.Descriptor,
            pending.Image.Value.AcquireRenderLease(),
            profile.Bytes));
    }

    private void PrepareCurrentManagedSource()
    {
        if (_image is null)
        {
            PublishPhotoPresentation();
            return;
        }

        var state = ClassifyMonitorState(_image.Value.Descriptor);
        SetMonitorColorState(state);
        if (state != MonitorColorState.Managed ||
            _displayProfile.Profile is not { } profile ||
            _managedPhotoCoordinator is not { } coordinator)
        {
            PublishPhotoPresentation();
            return;
        }

        var key = CreateManagedKey(_image.Value, profile.Identity);
        if (coordinator.TryAcquire(key, out var ready) && ready is not null)
        {
            var previous = _managedSource;
            _managedSource = ready;
            _requestedManagedKey = key;
            PublishPhotoPresentation();
            previous?.Dispose();
            return;
        }

        if (ManagedPhotoRequestPolicy.ShouldRequest(_requestedManagedKey, key))
        {
            _requestedManagedKey = key;
            coordinator.Request(new ManagedPhotoRenderRequest(
                key,
                _image.Value.Descriptor,
                _image.Value.AcquireRenderLease(),
                profile.Bytes));
        }

        PublishPhotoPresentation();
    }

    private void CommitPendingPresentation(ManagedPhotoSourceLease? managedSource, bool managedFailure)
    {
        var pending = _pendingPresentation;
        if (pending is null)
        {
            managedSource?.Dispose();
            return;
        }

        _pendingPresentation = null;
        var nextImage = pending.TakeImage();
        var nextAmbient = pending.TakeAmbient();
        var previousImage = _image;
        var previousAmbient = _ambient;
        var previousManagedSource = _managedSource;

        _image = nextImage;
        _canonicalImageIdentity = pending.Identity;
        _stage = pending.Stage;
        _ambient = nextAmbient;
        _ambientImageIdentity = nextAmbient is null ? null : pending.AmbientIdentity;
        _managedSource = managedSource;
        _requestedManagedKey = managedSource?.Source.Key;
        _managedPresentationFailed = managedFailure;
        _viewport.SetImage(nextImage.Value.Descriptor.OrientedSize, pending.Transfer);
        _presentation?.SelectImage(pending.Identity);
        PublishPhotoPresentation();
        UpdateMarkupOverlay();
        PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        _managedPhotoCoordinator?.RecordAtomicPresentationCommit(
            System.Diagnostics.Stopwatch.GetElapsedTime(pending.RequestedTimestamp));
        pending.Dispose();
        previousImage?.Dispose();
        previousAmbient?.Dispose();
        previousManagedSource?.Dispose();
        RaiseViewStateChanged();
    }

    private bool TryAcquirePublishedManagedSource(
        ManagedPhotoKey key,
        out ManagedPhotoSourceLease? source)
    {
        if (_managedSource is null || _managedSource.Source.Key != key)
        {
            source = null;
            return false;
        }

        source = _managedSource.Acquire();
        return true;
    }

    private MonitorColorState ClassifyMonitorState(ImageDescriptor descriptor) =>
        MonitorColorPolicy.Classify(
            _monitorColorManagementEnabled,
            _monitorColorPlatformSupported,
            _monitorColorEngineAvailable,
            _displayProfile,
            descriptor.ColorState);

    private bool TryCreateManagedKey(DecodedImage image, out ManagedPhotoKey key)
    {
        if (ClassifyMonitorState(image.Descriptor) != MonitorColorState.Managed ||
            _displayProfile.Profile is not { } profile)
        {
            key = default;
            return false;
        }

        key = CreateManagedKey(image, profile.Identity);
        return true;
    }

    private static ManagedPhotoKey CreateManagedKey(
        DecodedImage image,
        DisplayProfileIdentity destinationIdentity) => new(
        image.Identity,
        destinationIdentity,
        image.Descriptor.EncodedSize,
        image.Descriptor.Orientation);

    private static DecodedImage.AmbientLease? TakeMatchingAmbient(StagePresentation presentation)
    {
        var ambient = presentation.TakeAmbient();
        if (presentation.Stage.BackgroundMode.RequiresAmbient())
        {
            return ambient;
        }

        ambient?.Dispose();
        return null;
    }

    private void ReplaceCurrentStage(
        StageSettings stage,
        DecodedImage.AmbientLease? ambient,
        long? ambientIdentity)
    {
        var previous = _ambient;
        _stage = stage;
        _ambient = ambient;
        _ambientImageIdentity = ambientIdentity;
        previous?.Dispose();
    }

    private void DisposeManagedSource()
    {
        var source = _managedSource;
        _managedSource = null;
        source?.Dispose();
    }

    private void DisposePendingPresentation()
    {
        var pending = _pendingPresentation;
        _pendingPresentation = null;
        pending?.Dispose();
    }

    private void SetMonitorColorState(MonitorColorState state)
    {
        if (_monitorColorState == state)
        {
            return;
        }

        _monitorColorState = state;
        MonitorColorStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPhotoPresentationVisible(bool visible)
    {
        if (_photoPresentationVisible == visible)
        {
            return;
        }

        _photoPresentationVisible = visible;
        UpdateMarkupOverlay();
        UpdatePointerPresentation();
        ApplyCursor();
    }

    private void OnScalingChanged(object? sender, EventArgs e) => UpdateViewportMetrics();

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _lastPointerPosition = null;
        _pointerInside = false;
        _pointerFeedbackOverlay?.SetPointerPosition(null);
        ApplyCursor();
    }

    private void UpdateViewportMetrics()
    {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _viewport.SetViewport(
            new LogicalSize(Math.Max(Bounds.Width, 1), Math.Max(Bounds.Height, 1)),
            renderScaling);
        UpdatePointerPresentation();
        InvalidateAndReport();
    }

    private void NotifyPointerActivity() => PointerActivity?.Invoke(this, EventArgs.Empty);

    private void InvalidateAndReport()
    {
        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        RaiseViewStateChanged();
    }

    private void RaiseViewStateChanged() => ViewStateChanged?.Invoke(this, EventArgs.Empty);

    private void UpdatePointerPosition(PointerEventArgs e)
    {
        var pointer = e.GetPosition(this);
        _lastPointerPosition = new PointD(pointer.X, pointer.Y);
        _pointerFeedbackOverlay?.SetPointerPosition(_lastPointerPosition);
        if (!_pointerInside)
        {
            _pointerInside = true;
            ApplyCursor();
        }
    }

    private void DiscardInspection()
    {
        var comparison = _inspectionImage;
        var comparisonAmbient = _inspectionAmbient;
        _inspectionMode = InspectionMode.None;
        _inspectionRestore = null;
        _inspectionImage = null;
        _inspectionImageIdentity = null;
        _inspectionAmbient = null;
        _inspectionAmbientImageIdentity = null;
        _inspectionStage = null;
        _lastDragPoint = null;
        comparison?.Dispose();
        comparisonAmbient?.Dispose();
    }

    private void OnPresentationChanged(object? sender, PresentationChangedEventArgs e)
    {
        if (_presentation?.IsDrawing != true && _drawingPointer is { } pointer)
        {
            _drawingPointer = null;
            pointer.Capture(null);
        }

        var layers = InteractionRenderRouting.ForPresentationChange(e.Kind);
        if (layers.HasFlag(InteractionRenderLayer.Markup))
        {
            UpdateMarkupOverlay();
        }

        if (layers.HasFlag(InteractionRenderLayer.Pointer))
        {
            UpdatePointerPresentation();
            ApplyCursor();
        }
    }

    private void ApplyCursor()
    {
        if (_visibleViewerCursor is null && _hiddenViewerCursor is null)
        {
            return;
        }

        var feedback = GetPointerFeedback();
        var mode = ViewerSystemCursorPresentation.Resolve(_pointerInside, feedback.Kind);
        if (mode == _appliedSystemCursorMode)
        {
            return;
        }

        _appliedSystemCursorMode = mode;
        Cursor = mode switch
        {
            ViewerSystemCursorMode.Hand => _handViewerCursor ?? _visibleViewerCursor,
            ViewerSystemCursorMode.Hidden => _hiddenViewerCursor,
            _ => _visibleViewerCursor,
        };
    }

    private DrawingCursorPresentation GetPointerFeedback()
    {
        if (!_photoPresentationVisible || _presentation is not { } presentation)
        {
            return default;
        }

        if (presentation.TemporaryHandActive)
        {
            return DrawingCursorPresentation.Resolve(
                true,
                presentation.HighlightEnabled,
                MarkupTool.Hand,
                presentation.ActiveStrokePhysicalPixels,
                presentation.ActiveColor,
                presentation.ActiveOpacity,
                presentation.Settings.HighlightRadiusPhysicalPixels,
                _viewport.RenderScaling);
        }

        if (_photoPresentationView.IsEnabled &&
            presentation.MarkupToolsVisible &&
            presentation.EffectiveTool == MarkupTool.Hand)
        {
            return DrawingCursorPresentation.Resolve(
                false,
                presentation.HighlightEnabled,
                MarkupTool.Hand,
                presentation.ActiveStrokePhysicalPixels,
                presentation.Settings.HighlightColor,
                presentation.Settings.HighlightOpacity,
                presentation.Settings.HighlightRadiusPhysicalPixels,
                _viewport.RenderScaling);
        }

        if (_colorPickerEnabled)
        {
            return DrawingCursorPresentation.CreateColorPicker(_viewport.RenderScaling);
        }

        var useMarkupStyle = presentation.MarkupToolsVisible;
        var color = useMarkupStyle
            ? presentation.ActiveColor
            : presentation.Settings.HighlightColor;
        var opacity = useMarkupStyle
            ? presentation.ActiveOpacity
            : presentation.Settings.HighlightOpacity;
        return DrawingCursorPresentation.Resolve(
            presentation.MarkupToolsVisible,
            presentation.HighlightEnabled,
            presentation.EffectiveTool,
            presentation.ActiveStrokePhysicalPixels,
            color,
            opacity,
            presentation.Settings.HighlightRadiusPhysicalPixels,
            _viewport.RenderScaling);
    }

    private void UpdatePointerPresentation()
    {
        _pointerFeedbackOverlay?.SetPresentation(GetPointerFeedback());
    }

    private void UpdateMarkupOverlay()
    {
        if (_markupOverlay is null)
        {
            return;
        }

        var image = _inspectionImage ?? _image;
        if (!_photoPresentationVisible || image is null || _presentation is null)
        {
            _markupOverlay.SetPresentation(null);
            return;
        }

        var identity = PresentedImageIdentity;
        var snapshot = _presentation.GetRenderSnapshot(identity);
        _markupOverlay.SetPresentation(new MarkupOverlayFrame(
            GetDestination(),
            image.Value.Descriptor.OrientedSize,
            snapshot));
    }

    private PointD? TryGetSourcePoint(Point pointer)
    {
        if (!_photoPresentationVisible)
        {
            return null;
        }

        var destination = GetDestination();
        if (pointer.X < destination.X || pointer.X > destination.X + destination.Width ||
            pointer.Y < destination.Y || pointer.Y > destination.Y + destination.Height)
        {
            return null;
        }

        return MapViewportPointToSource(new PointD(pointer.X, pointer.Y));
    }

    private void RequestColorSample(Point pointer)
    {
        var handler = ColorSampleRequested;
        if (!_photoPresentationVisible ||
            handler is null ||
            !TryAcquirePresentedImage(out var presented) ||
            presented is null)
        {
            return;
        }

        using (presented)
        {
            var sourceSize = presented.Image.Descriptor.OrientedSize;
            if (!PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
                    GetDestination(),
                    sourceSize,
                    new PointD(pointer.X, pointer.Y),
                    out var pixel))
            {
                return;
            }

            handler(this, new PhotoSampleRequestedEventArgs(presented, pixel));
        }
    }

    private PointD GetClampedSourcePoint(Point pointer)
    {
        var source = MapViewportPointToSource(new PointD(pointer.X, pointer.Y));
        var size = GetPresentedOrientedSize();
        return new PointD(
            Math.Clamp(source.X, 0, size.Width),
            Math.Clamp(source.Y, 0, size.Height));
    }

    private PointD MapViewportPointToSource(PointD point)
    {
        if (!_photoPresentationView.IsEnabled)
        {
            return _viewport.SourcePointAt(point);
        }

        var destination = GetDestination();
        var size = GetPresentedOrientedSize();
        return new PointD(
            (point.X - destination.X) * size.Width / destination.Width,
            (point.Y - destination.Y) * size.Height / destination.Height);
    }

    private static MarkupDrawingModifiers ToDrawingModifiers(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Shift)
            ? MarkupDrawingModifiers.Constrain
            : MarkupDrawingModifiers.None;
}
