using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Fovium.Diagnostics;
using Fovium.ColorManagement;
using Fovium.ColorPicking;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Viewer;

internal readonly record struct ViewportAmbientPresentationState(
    long? ImageIdentity,
    long? AmbientIdentity,
    StageBackgroundMode BackgroundMode,
    bool HasMatchingAmbient);

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
    private DisplayProfileResolution _displayProfile = new(
        MonitorColorState.DestinationUnavailable,
        null,
        "No display profile has been resolved.");
    private ManagedPhotoKey? _requestedManagedKey;
    private bool _monitorColorManagementEnabled = true;
    private bool _monitorColorEngineAvailable;
    private bool _managedPresentationFailed;
    private MonitorColorState _monitorColorState = MonitorColorState.PlatformUnsupported;
    private bool _photoPresentationVisible;

    public PhotoViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;
        CacheMode = new BitmapCache { SnapsToDevicePixels = true };
    }

    public event EventHandler? PointerActivity;

    public event EventHandler? ViewStateChanged;

    public event EventHandler? PresentedImageChanged;

    public event EventHandler<PhotoSampleRequestedEventArgs>? ColorSampleRequested;

    public event EventHandler? MonitorColorStateChanged;

    public bool HasImage => _image is not null;

    public InspectionMode InspectionMode => _inspectionMode;

    internal MonitorColorState MonitorColorState => _monitorColorState;

    internal ManagedPhotoCoordinatorMetrics? MonitorColorMetrics => _managedPhotoCoordinator?.Metrics;

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
        _image is null ? ViewTransfer.Fit : _viewport.CaptureTransfer();

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
        PublishPhotoPresentation();
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
        PublishPhotoPresentation();
    }

    internal void SetDisplayProfile(DisplayProfileResolution profile)
    {
        var unchanged = _displayProfile.State == profile.State &&
            _displayProfile.AdvancedColorEnabled == profile.AdvancedColorEnabled &&
            _displayProfile.Profile?.Identity == profile.Profile?.Identity;
        _displayProfile = profile;
        if (unchanged)
        {
            return;
        }

        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        _managedPhotoCoordinator?.Clear();
        PublishPhotoPresentation();
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
        PublishPhotoPresentation();
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

        var ambient = presentation.TakeAmbient();
        if (!presentation.Stage.BackgroundMode.RequiresAmbient())
        {
            ambient?.Dispose();
            ambient = null;
        }

        DiscardInspection();
        var previousImage = _image;
        var previousAmbient = _ambient;
        _image = image;
        _canonicalImageIdentity = imageIdentity;
        _stage = presentation.Stage;
        _ambient = ambient;
        _ambientImageIdentity = ambient is null ? null : presentation.ImageIdentity;
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, transfer);
        ResetManagedPhotoRequest();
        _presentation?.SelectImage(imageIdentity);
        UpdateMarkupOverlay();
        PublishPhotoPresentation();
        PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        previousImage?.Dispose();
        previousAmbient?.Dispose();
        RaiseViewStateChanged();
    }

    public void SetStage(StagePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var ambient = presentation.TakeAmbient();
        var currentIdentity = _image?.Value.Identity;
        if (!presentation.Stage.BackgroundMode.RequiresAmbient() ||
            presentation.ImageIdentity != currentIdentity)
        {
            ambient?.Dispose();
            ambient = null;
        }

        var previous = _ambient;
        _stage = presentation.Stage;
        _ambient = ambient;
        _ambientImageIdentity = ambient is null ? null : presentation.ImageIdentity;
        PublishPhotoPresentation();
        previous?.Dispose();
    }

    public void ClearImage()
    {
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
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
        if (_image is null)
        {
            return;
        }

        _viewport.Fit();
        InvalidateAndReport();
    }

    public void SetPhotographic100AtCenter()
    {
        if (_image is null)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomAt(center, 1);
        InvalidateAndReport();
    }

    public void ZoomByStepsAtCenter(int steps)
    {
        if (_image is null || steps == 0)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomBySteps(center, steps);
        InvalidateAndReport();
    }

    public bool BeginPeek100()
    {
        if (_image is null || _inspectionMode != InspectionMode.None)
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
        if (_image is null || _inspectionMode != InspectionMode.None)
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
            _lastDragPoint = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (colorPickerAction == ColorPickerPrimaryClickAction.Sample)
        {
            RequestColorSample(e.GetPosition(this));
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
                _lastDragPoint = pointer;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            var source = TryGetSourcePoint(pointer);
            if (source is not null && presentation.BeginDrawing(
                    source.Value,
                    _viewport.PhysicalScale,
                    _viewport.SourceSize))
            {
                _drawingPointer = e.Pointer;
                e.Pointer.Capture(this);
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            var pointer = e.GetPosition(this);
            _viewport.ToggleFitAnd100(new PointD(pointer.X, pointer.Y));
            _lastDragPoint = null;
            InvalidateAndReport();
            e.Handled = true;
            return;
        }

        _lastDragPoint = e.GetPosition(this);
        e.Pointer.Capture(this);
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
        var destination = _viewport.DestinationDip;
        if (!_viewport.UsesExactPixelSampling)
        {
            return destination;
        }

        var aligned = _viewport.PhysicalAlignedOrigin();
        return destination with { X = aligned.X, Y = aligned.Y };
    }

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
        SharedResourceLease<ManagedPhotoSurface>? managedSurface = null;
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
            var state = MonitorColorPolicy.Classify(
                _monitorColorManagementEnabled,
                OperatingSystem.IsWindows(),
                _monitorColorEngineAvailable,
                _displayProfile,
                descriptor.ColorState);
            if (_managedPresentationFailed && state == MonitorColorState.Managed)
            {
                state = MonitorColorState.InvalidDestinationProfile;
            }

            if (state == MonitorColorState.Managed &&
                _displayProfile.Profile is { } profile &&
                _managedPhotoCoordinator is { } coordinator)
            {
                var geometry = new ManagedPhotoGeometry(
                    new RectD(0, 0, Bounds.Width, Bounds.Height),
                    destination,
                    _viewport.RenderScaling,
                    _viewport.UsesExactPixelSampling);
                if (geometry.IsValid)
                {
                    var key = new ManagedPhotoKey(
                        presentedNumericIdentity,
                        profile.Identity,
                        descriptor.EncodedSize,
                        descriptor.Orientation,
                        geometry);
                    if (!coordinator.TryAcquire(key, out managedSurface))
                    {
                        suppressLegacyPhoto = true;
                        if (_requestedManagedKey != key)
                        {
                            _requestedManagedKey = key;
                            coordinator.Request(new ManagedPhotoRenderRequest(
                                key,
                                descriptor,
                                cachedLease.Value.AcquireRenderLease(),
                                profile.Bytes));
                        }
                    }
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
                _viewport.UsesExactPixelSampling,
                presentationStage,
                _viewport.RenderScaling,
                ambientLease,
                presentedNumericIdentity,
                ambientLease is null ? null : ambientIdentity,
                _ambientFrameDiagnostics,
                _interactionDiagnostics,
                managedSurface,
                suppressLegacyPhoto));
            SetPhotoPresentationVisible(!suppressLegacyPhoto);
            renderLease = null;
            ambientLease = null;
            managedSurface = null;
        }
        finally
        {
            renderLease?.Dispose();
            ambientLease?.Dispose();
            managedSurface?.Dispose();
        }
    }

    private void ResetManagedPhotoRequest()
    {
        _requestedManagedKey = null;
        _managedPresentationFailed = false;
        _managedPhotoCoordinator?.Clear();
    }

    private void OnManagedPresentationChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(PublishPhotoPresentation);

    private void OnManagedPresentationFailed(object? sender, EventArgs e)
    {
        _managedPresentationFailed = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(PublishPhotoPresentation);
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

        return _viewport.SourcePointAt(new PointD(pointer.X, pointer.Y));
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
        var source = _viewport.SourcePointAt(new PointD(pointer.X, pointer.Y));
        var size = _viewport.SourceSize;
        return new PointD(
            Math.Clamp(source.X, 0, size.Width),
            Math.Clamp(source.Y, 0, size.Height));
    }

    private static MarkupDrawingModifiers ToDrawingModifiers(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Shift)
            ? MarkupDrawingModifiers.Constrain
            : MarkupDrawingModifiers.None;
}
