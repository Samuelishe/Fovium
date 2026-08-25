using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
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

internal sealed class PhotoViewportControl : Control
{
    private readonly ViewportModel _viewport = new();
    private readonly AmbientRenderFrameDiagnostics _ambientFrameDiagnostics = new();
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
    private string? _canonicalImageIdentity;
    private Cursor? _visibleViewerCursor;
    private Cursor? _hiddenViewerCursor;
    private Cursor? _handViewerCursor;

    public PhotoViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public event EventHandler? PointerActivity;

    public event EventHandler? ViewStateChanged;

    public bool HasImage => _image is not null;

    public InspectionMode InspectionMode => _inspectionMode;

    internal string? PresentedImageIdentity => _inspectionImage is not null
        ? _inspectionImageIdentity
        : _canonicalImageIdentity;

    internal MarkupRenderSnapshot CapturePresentedMarkup() =>
        _presentation?.GetRenderSnapshot(PresentedImageIdentity) ?? MarkupRenderSnapshot.Empty;

    public ViewTransfer CaptureViewTransfer() =>
        _image is null ? ViewTransfer.Fit : _viewport.CaptureTransfer();

    internal AmbientRenderFrameMetrics GetAmbientRenderFrameMetrics() =>
        _ambientFrameDiagnostics.GetMetrics();

    internal void EnableAmbientPipelineDiagnostics() =>
        _ambientFrameDiagnostics.EnablePipelineTracking();

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
        Cursor? handViewerCursor = null)
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
        presentation.Changed += OnPresentationChanged;
        ApplyCursor();
        InvalidateVisual();
    }

    public void SetViewerCursor(Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        _visibleViewerCursor = cursor;
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
        _presentation?.SelectImage(imageIdentity);
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, transfer);
        InvalidateVisual();
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
        _presentation?.SelectImage(imageIdentity);
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, transfer);
        InvalidateVisual();
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
        InvalidateVisual();
        previous?.Dispose();
    }

    public void ClearImage()
    {
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
        _image = null;
        _canonicalImageIdentity = null;
        _presentation?.SelectImage(null);
        _ambient = null;
        _ambientImageIdentity = null;
        InvalidateVisual();
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
        InvalidateVisual();
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
        InvalidateVisual();
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

        InvalidateVisual();
        comparison?.Dispose();
        comparisonAmbient?.Dispose();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _ambientFrameDiagnostics.RecordViewportRender();
        var presentationStage = _inspectionImage is not null ? _inspectionStage! : _stage;
        var fallbackColor = presentationStage.BackgroundMode switch
        {
            StageBackgroundMode.Neutral => StageDefaults.NeutralColor,
            StageBackgroundMode.Custom => presentationStage.CustomBackgroundColor,
            _ => StageDefaults.BlackColor,
        };
        IBrush fallback = new SolidColorBrush(Color.FromRgb(
            fallbackColor.Red,
            fallbackColor.Green,
            fallbackColor.Blue));
        context.FillRectangle(fallback, new Rect(Bounds.Size));
        var cachedLease = _inspectionImage ?? _image;
        if (cachedLease is not null)
        {
            DecodedImage.RenderLease? renderLease = null;
            DecodedImage.AmbientLease? ambientLease = null;
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
                var presentationIdentity = _inspectionImage is not null
                    ? _inspectionImageIdentity
                    : _canonicalImageIdentity;
                var markup = _presentation?.GetRenderSnapshot(presentationIdentity)
                    ?? MarkupRenderSnapshot.Empty;
                _ambientFrameDiagnostics.RecordCustomDrawScheduled();
                context.Custom(new SkiaPhotoDrawOperation(
                    new Rect(Bounds.Size),
                    renderLease,
                    descriptor.EncodedSize,
                    descriptor.Orientation,
                    GetDestination(),
                    _viewport.UsesExactPixelSampling,
                    presentationStage,
                    _viewport.RenderScaling,
                    ambientLease,
                    markup,
                    presentedNumericIdentity,
                    ambientLease is null ? null : ambientIdentity,
                    _ambientFrameDiagnostics));
                renderLease = null;
                ambientLease = null;
            }
            finally
            {
                renderLease?.Dispose();
                ambientLease?.Dispose();
            }
        }

        if (_presentation is { } pointerPresentation &&
            _pointerInside &&
            _lastPointerPosition is { } pointerPosition)
        {
            DrawPointerFeedback(context, pointerPosition);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
        }

        UpdateViewportMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
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
            InvalidateVisual();
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

    private void OnScalingChanged(object? sender, EventArgs e) => UpdateViewportMetrics();

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _lastPointerPosition = null;
        _pointerInside = false;
        ApplyCursor();
        InvalidateVisual();
    }

    private void UpdateViewportMetrics()
    {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _viewport.SetViewport(
            new LogicalSize(Math.Max(Bounds.Width, 1), Math.Max(Bounds.Height, 1)),
            renderScaling);
        InvalidateAndReport();
    }

    private void NotifyPointerActivity() => PointerActivity?.Invoke(this, EventArgs.Empty);

    private void InvalidateAndReport()
    {
        InvalidateVisual();
        RaiseViewStateChanged();
    }

    private void RaiseViewStateChanged() => ViewStateChanged?.Invoke(this, EventArgs.Empty);

    private void UpdatePointerPosition(PointerEventArgs e)
    {
        var pointer = e.GetPosition(this);
        _lastPointerPosition = new PointD(pointer.X, pointer.Y);
        _pointerInside = true;
        ApplyCursor();
        if (_presentation is { } presentation &&
            (presentation.HighlightEnabled ||
                presentation.MarkupToolsVisible))
        {
            InvalidateVisual();
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

    private void OnPresentationChanged(object? sender, EventArgs e)
    {
        if (_presentation?.IsDrawing != true && _drawingPointer is { } pointer)
        {
            _drawingPointer = null;
            pointer.Capture(null);
        }

        ApplyCursor();
        InvalidateVisual();
    }

    private void ApplyCursor()
    {
        if (_visibleViewerCursor is null && _hiddenViewerCursor is null)
        {
            return;
        }

        var feedback = GetPointerFeedback();
        Cursor = feedback.Kind switch
        {
            DrawingCursorKind.Hand when _pointerInside => _handViewerCursor ?? _visibleViewerCursor,
            DrawingCursorKind.Highlight or
                DrawingCursorKind.Brush or
                DrawingCursorKind.Eraser or
                DrawingCursorKind.Precision when _pointerInside => _hiddenViewerCursor,
            _ => _visibleViewerCursor,
        };
    }

    private DrawingCursorPresentation GetPointerFeedback()
    {
        if (_presentation is not { } presentation)
        {
            return default;
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

    private void DrawPointerFeedback(
        DrawingContext context,
        PointD pointer)
    {
        var feedback = GetPointerFeedback();
        var center = new Point(pointer.X, pointer.Y);
        switch (feedback.Kind)
        {
            case DrawingCursorKind.Highlight:
            case DrawingCursorKind.Brush:
                {
                    var alpha = (byte)Math.Round(feedback.Opacity * byte.MaxValue);
                    var color = feedback.Color;
                    var fill = new SolidColorBrush(Color.FromArgb(alpha, color.Red, color.Green, color.Blue));
                    var radius = feedback.DiameterDip / 2;
                    context.DrawEllipse(fill, null, center, radius, radius);
                    if (feedback.Kind == DrawingCursorKind.Brush)
                    {
                        DrawHighContrastCircle(context, center, radius, feedback.OutlineWidthDip);
                    }

                    break;
                }

            case DrawingCursorKind.Eraser:
                DrawHighContrastCircle(
                    context,
                    center,
                    feedback.DiameterDip / 2,
                    feedback.OutlineWidthDip);
                break;

            case DrawingCursorKind.Precision:
                DrawPrecisionCursor(context, center, feedback);
                break;
        }
    }

    private static void DrawHighContrastCircle(
        DrawingContext context,
        Point center,
        double radius,
        double outlineWidth)
    {
        context.DrawEllipse(null, new Pen(Brushes.Black, outlineWidth * 3), center, radius, radius);
        context.DrawEllipse(null, new Pen(Brushes.White, outlineWidth), center, radius, radius);
    }

    private static void DrawPrecisionCursor(
        DrawingContext context,
        Point center,
        DrawingCursorPresentation feedback)
    {
        var half = feedback.CrosshairHalfExtentDip;
        var gap = Math.Min(half / 3, 3 * feedback.OutlineWidthDip);
        var segments = new[]
        {
            (new Point(center.X - half, center.Y), new Point(center.X - gap, center.Y)),
            (new Point(center.X + gap, center.Y), new Point(center.X + half, center.Y)),
            (new Point(center.X, center.Y - half), new Point(center.X, center.Y - gap)),
            (new Point(center.X, center.Y + gap), new Point(center.X, center.Y + half)),
        };
        foreach (var (start, end) in segments)
        {
            context.DrawLine(new Pen(Brushes.Black, feedback.OutlineWidthDip * 3), start, end);
            context.DrawLine(new Pen(Brushes.White, feedback.OutlineWidthDip), start, end);
        }
    }

    private PointD? TryGetSourcePoint(Point pointer)
    {
        var destination = GetDestination();
        if (pointer.X < destination.X || pointer.X > destination.X + destination.Width ||
            pointer.Y < destination.Y || pointer.Y > destination.Y + destination.Height)
        {
            return null;
        }

        return _viewport.SourcePointAt(new PointD(pointer.X, pointer.Y));
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
