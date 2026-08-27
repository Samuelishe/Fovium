using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace Fovium.ColorManagementProbe;

internal sealed record AvaloniaTargetEvidence(
    bool LeaseAvailable,
    bool LeaseSurfaceAvailable,
    bool CanvasSurfaceAvailable,
    bool GrContextAvailable,
    bool RuntimeShaderCompiled,
    bool RuntimeShaderDrawn,
    string? RuntimeShaderError,
    int RuntimeShaderDrawIterations,
    double RuntimeShaderDrawMilliseconds,
    string SurfaceColorSpace,
    string SnapshotColorSpace,
    string CanvasType,
    string? PlatformHandleDescriptor);

internal sealed class TargetProbeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new TargetProbeWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class TargetProbeWindow : Window
{
    private readonly TargetProbeControl _probe = new();

    public TargetProbeWindow()
    {
        Title = "Fovium color-management target probe";
        Width = 640;
        Height = 360;
        Content = _probe;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(_probe.InvalidateVisual, DispatcherPriority.Render);
        await Task.Delay(1200);
        var platformHandle = TryGetPlatformHandle();
        var windows = platformHandle is not null && platformHandle.HandleDescriptor == "HWND"
            ? WindowsDisplayProfileProbe.ReadForWindow(platformHandle.Handle)
            : default;
        ProbeReporter.WriteAvaloniaEvidence(
            _probe.Evidence,
            platformHandle?.HandleDescriptor,
            windows);
        Close();
    }
}

internal sealed class TargetProbeControl : Control
{
    public AvaloniaTargetEvidence? Evidence { get; private set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new TargetDrawOperation(Bounds, evidence => Evidence = evidence));
    }

    private sealed class TargetDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly Action<AvaloniaTargetEvidence> _record;

        public TargetDrawOperation(Rect bounds, Action<AvaloniaTargetEvidence> record)
        {
            _bounds = bounds;
            _record = record;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point point) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                _record(new AvaloniaTargetEvidence(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    null,
                    0,
                    0,
                    "unavailable",
                    "unavailable",
                    "unavailable",
                    null));
                return;
            }

            using var lease = feature.Lease();
            var leaseSurface = lease.SkSurface;
            var canvasSurface = lease.SkCanvas.Surface;
            var surface = leaseSurface ?? canvasSurface;
            var surfaceColorSpace = "unavailable";
            var snapshotColorSpace = "unavailable";
            if (surface is not null)
            {
                using var pixels = surface.PeekPixels();
                surfaceColorSpace = Describe(pixels?.ColorSpace);
                using var snapshot = surface.Snapshot();
                snapshotColorSpace = Describe(snapshot.ColorSpace);
            }

            lease.SkCanvas.Clear(new SKColor(31, 31, 31));
            const string shaderSource = "half4 main(float2 position) { return half4(position.x / 640.0, position.y / 360.0, 0.25, 1.0); }";
            using var effect = SKRuntimeEffect.CreateShader(shaderSource, out var shaderError);
            var runtimeShaderCompiled = effect is not null;
            var runtimeShaderDrawn = false;
            const int runtimeShaderDrawIterations = 200;
            var runtimeShaderDrawMilliseconds = 0d;
            if (effect is not null)
            {
                using var shader = effect.ToShader();
                using var paint = new SKPaint { Shader = shader };
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                for (var iteration = 0; iteration < runtimeShaderDrawIterations; iteration++)
                {
                    lease.SkCanvas.DrawRect(new SKRect(0, 0, 640, 360), paint);
                }

                lease.SkCanvas.Flush();
                stopwatch.Stop();
                runtimeShaderDrawMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                runtimeShaderDrawn = true;
            }

            _record(new AvaloniaTargetEvidence(
                true,
                leaseSurface is not null,
                canvasSurface is not null,
                lease.GrContext is not null,
                runtimeShaderCompiled,
                runtimeShaderDrawn,
                string.IsNullOrWhiteSpace(shaderError) ? null : shaderError,
                runtimeShaderDrawIterations,
                runtimeShaderDrawMilliseconds,
                surfaceColorSpace,
                snapshotColorSpace,
                lease.SkCanvas.GetType().FullName ?? lease.SkCanvas.GetType().Name,
                null));
        }

        private static string Describe(SKColorSpace? colorSpace) => colorSpace is null
            ? "null/untagged"
            : colorSpace.IsSrgb
                ? "sRGB"
                : "non-sRGB";
    }
}
