using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Fovium.RenderProbe;

internal sealed class RenderProbeControl : Control
{
    private readonly ViewportModel _viewport = new();
    private ProbeImage? _image;
    private Point? _lastPointer;
    private TopLevel? _topLevel;

    public RenderProbeControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public event EventHandler? DiagnosticsChanged;

    public RenderPath RenderPath { get; set; } = RenderPath.DirectSkia;

    public SamplingMode SamplingMode { get; set; } = SamplingMode.LinearMipmap;

    public string? LastError { get; private set; }

    public void SetImage(ProbeImage image)
    {
        var previous = _image;
        _image = image;
        LastError = null;
        _viewport.SetImage(image.Diagnostics.OrientedSize);
        InvalidateVisual();
        RaiseDiagnosticsChanged();
        previous?.Dispose();
    }

    public void SetError(Exception exception)
    {
        LastError = exception.Message;
        RaiseDiagnosticsChanged();
    }

    public void Fit()
    {
        _viewport.Fit();
        InvalidateAndReport();
    }

    public void SetPhotographic100()
    {
        _viewport.SetPhotographic100();
        InvalidateAndReport();
    }

    public void SetPhysicalScale(double physicalScale)
    {
        _viewport.SetPhysicalScaleCentered(physicalScale);
        InvalidateAndReport();
    }

    public void RefreshRendering()
    {
        InvalidateAndReport();
    }

    public void DisposeImage()
    {
        _image?.Dispose();
        _image = null;
    }

    public string GetDiagnostics()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Render path: {RenderPath}");
        builder.AppendLine($"Sampling: {SamplingMode}{GetAvaloniaSamplingNote()}");
        builder.AppendLine($"Viewport: {Bounds.Width:F1} × {Bounds.Height:F1} DIP");
        builder.AppendLine($"RenderScaling: {_viewport.RenderScaling:F2}");
        builder.AppendLine(
            $"Viewport physical: {Bounds.Width * _viewport.RenderScaling:F0} × " +
            $"{Bounds.Height * _viewport.RenderScaling:F0} px");
        builder.AppendLine($"Physical zoom: {_viewport.PhysicalScale * 100:F2}%");
        builder.AppendLine($"DIP scale: {_viewport.DipScale:F6}");

        if (_image is not null)
        {
            var info = _image.Diagnostics;
            builder.AppendLine($"Source: {info.Source}");
            builder.AppendLine($"Decoder comparison: {info.DecoderPath}");
            builder.AppendLine($"Encoded: {info.EncodedSize.Width} × {info.EncodedSize.Height}");
            builder.AppendLine($"Oriented: {info.OrientedSize.Width} × {info.OrientedSize.Height}");
            builder.AppendLine($"Orientation: {(int)info.Orientation} ({info.Orientation})");
            builder.AppendLine($"Format / frames: {info.EncodedFormat} / {info.FrameCount}");
            builder.AppendLine($"Pixel / alpha: {info.PixelFormat} / {info.AlphaType}");
            builder.AppendLine($"Color: {info.ColorState}");
            builder.AppendLine($"Raw embedded ICC exposed: {info.RawEmbeddedProfileAvailable}");
            builder.AppendLine($"Reduced decode advertised: {info.ReducedDecodeAdvertised}");
            builder.AppendLine($"Estimated two-copy decode: {FormatBytes(info.EstimatedWorkingBytes)}");
            builder.AppendLine($"Header/probe: {info.HeaderMilliseconds:F2} ms");
            builder.AppendLine($"SKCodec decode: {info.SkiaDecodeMilliseconds:F2} ms");
            builder.AppendLine($"Avalonia Bitmap decode: {info.AvaloniaDecodeMilliseconds:F2} ms");
            builder.AppendLine($"Image preparation: {info.PreparationMilliseconds:F2} ms");
        }

        if (LastError is not null)
        {
            builder.AppendLine($"Error: {LastError}");
        }

        return builder.ToString().TrimEnd();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        if (_image is null)
        {
            return;
        }

        var destination = GetAlignedDestination();
        if (RenderPath == RenderPath.DirectSkia)
        {
            context.Custom(new SkiaImageDrawOperation(
                new Rect(Bounds.Size),
                _image.SkiaImage,
                _image.Diagnostics.EncodedSize,
                _image.Diagnostics.Orientation,
                destination,
                SamplingMode));
            return;
        }

        var affine = OrientationAffine.Create(
            _image.Diagnostics.EncodedSize,
            _image.Diagnostics.Orientation);
        var orientedSize = _image.Diagnostics.OrientedSize;
        var scaleX = destination.Width / orientedSize.Width;
        var scaleY = destination.Height / orientedSize.Height;
        var matrix = new Matrix(
            affine.A * scaleX,
            affine.D * scaleY,
            affine.B * scaleX,
            affine.E * scaleY,
            destination.X + affine.C * scaleX,
            destination.Y + affine.F * scaleY);

        using (context.PushRenderOptions(new RenderOptions
        {
            BitmapInterpolationMode = ToAvaloniaInterpolation(SamplingMode),
        }))
        using (context.PushTransform(matrix))
        {
            context.DrawImage(
                _image.AvaloniaBitmap,
                new Rect(0, 0, _image.Diagnostics.EncodedSize.Width, _image.Diagnostics.EncodedSize.Height));
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
        var cursor = e.GetPosition(this);
        var factor = Math.Pow(1.25, e.Delta.Y);
        var newScale = Math.Clamp(_viewport.PhysicalScale * factor, 0.01, 32);
        _viewport.ZoomAt(new PointD(cursor.X, cursor.Y), newScale);
        InvalidateAndReport();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _lastPointer = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointer is not { } previous || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _viewport.PanBy(new PointD(current.X - previous.X, current.Y - previous.Y));
        _lastPointer = current;
        InvalidateAndReport();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_lastPointer is not null)
        {
            _lastPointer = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnScalingChanged(object? sender, EventArgs e) => UpdateViewportMetrics();

    private void UpdateViewportMetrics()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _viewport.SetViewport(new LogicalSize(Math.Max(Bounds.Width, 1), Math.Max(Bounds.Height, 1)), scaling);
        InvalidateAndReport();
    }

    private RectD GetAlignedDestination()
    {
        var destination = _viewport.DestinationDip;
        if (Math.Abs(_viewport.PhysicalScale - Math.Round(_viewport.PhysicalScale)) > 1e-9)
        {
            return destination;
        }

        var aligned = _viewport.PhysicalAlignedOrigin();
        return destination with { X = aligned.X, Y = aligned.Y };
    }

    private void InvalidateAndReport()
    {
        InvalidateVisual();
        RaiseDiagnosticsChanged();
    }

    private void RaiseDiagnosticsChanged() => DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

    private string GetAvaloniaSamplingNote() =>
        RenderPath == RenderPath.AvaloniaDrawingContext &&
        SamplingMode is SamplingMode.Mitchell or SamplingMode.CatmullRom
            ? " (Avalonia HighQuality; cubic choice is not separately exposed)"
            : string.Empty;

    private static BitmapInterpolationMode ToAvaloniaInterpolation(SamplingMode sampling) =>
        sampling switch
        {
            SamplingMode.Nearest => BitmapInterpolationMode.None,
            SamplingMode.Linear => BitmapInterpolationMode.LowQuality,
            SamplingMode.LinearMipmap => BitmapInterpolationMode.MediumQuality,
            SamplingMode.Mitchell or SamplingMode.CatmullRom => BitmapInterpolationMode.HighQuality,
            _ => throw new ArgumentOutOfRangeException(nameof(sampling)),
        };

    private static string FormatBytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("F1", CultureInfo.InvariantCulture) + " MiB";
}
