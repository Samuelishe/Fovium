using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Fovium.ColorManagementProbe;

internal readonly record struct ProbePixel(byte Red, byte Green, byte Blue, byte Alpha);

internal readonly record struct TransformBenchmark(
    int SourceWidth,
    int SourceHeight,
    int DestinationWidth,
    int DestinationHeight,
    long SourceRasterBytes,
    long DestinationRasterBytes,
    TimeSpan SurfaceCreation,
    TimeSpan Draw,
    TimeSpan Snapshot);

internal readonly record struct TransformMemoryObservation(
    int SourceWidth,
    int SourceHeight,
    int DestinationWidth,
    int DestinationHeight,
    long PrivateBytesBefore,
    long PrivateBytesWithSource,
    long PrivateBytesWithDestination,
    long SourceDelta,
    long DestinationDelta,
    long TotalDelta);

internal static class SkiaColorTransformProbe
{
    public static byte[]? TrySerialize(SKColorSpace colorSpace)
    {
        using var profile = colorSpace.ToProfile();
        if (profile.Size <= 0 || profile.Size > IccProfileInspector.MaximumProfileBytes || profile.Buffer == 0)
        {
            return null;
        }

        var bytes = new byte[checked((int)profile.Size)];
        Marshal.Copy(profile.Buffer, bytes, 0, bytes.Length);
        return bytes;
    }

    public static ProbePixel TransformPixel(
        ProbePixel source,
        SKColorSpace sourceColorSpace,
        SKColorSpace destinationColorSpace)
    {
        using var bitmap = CreatePatchBitmap(1, 1, sourceColorSpace, source);
        using var image = SKImage.FromBitmap(bitmap);
        var destinationInfo = new SKImageInfo(
            1,
            1,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            destinationColorSpace);
        using var surface = SKSurface.Create(destinationInfo)
            ?? throw new InvalidOperationException("Skia could not create the destination surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(image, 0, 0);
        surface.Canvas.Flush();
        Span<byte> destination = stackalloc byte[4];
        unsafe
        {
            fixed (byte* destinationPointer = destination)
            {
                if (!surface.ReadPixels(destinationInfo, (nint)destinationPointer, 4, 0, 0))
                {
                    throw new InvalidOperationException("Skia could not read the transformed pixel.");
                }
            }
        }

        return Unpremultiply(destination[2], destination[1], destination[0], destination[3]);
    }

    public static ProbePixel DrawToUntaggedSurface(ProbePixel source, SKColorSpace sourceColorSpace)
    {
        using var bitmap = CreatePatchBitmap(1, 1, sourceColorSpace, source);
        using var image = SKImage.FromBitmap(bitmap);
        var destinationInfo = new SKImageInfo(
            1,
            1,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(destinationInfo)
            ?? throw new InvalidOperationException("Skia could not create the untagged destination surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(image, 0, 0);
        surface.Canvas.Flush();
        Span<byte> destination = stackalloc byte[4];
        unsafe
        {
            fixed (byte* destinationPointer = destination)
            {
                if (!surface.ReadPixels(destinationInfo, (nint)destinationPointer, 4, 0, 0))
                {
                    throw new InvalidOperationException("Skia could not read the untagged destination pixel.");
                }
            }
        }

        return Unpremultiply(destination[2], destination[1], destination[0], destination[3]);
    }

    public static TransformBenchmark Benchmark(
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        SKColorSpace sourceColorSpace,
        SKColorSpace destinationColorSpace,
        bool retainSnapshot)
    {
        using var bitmap = CreatePatchBitmap(
            sourceWidth,
            sourceHeight,
            sourceColorSpace,
            new ProbePixel(196, 83, 41, 255));
        using var image = SKImage.FromBitmap(bitmap);
        var destinationInfo = new SKImageInfo(
            destinationWidth,
            destinationHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            destinationColorSpace);

        var watch = Stopwatch.StartNew();
        using var surface = SKSurface.Create(destinationInfo)
            ?? throw new InvalidOperationException("Skia could not create the benchmark surface.");
        watch.Stop();
        var creation = watch.Elapsed;

        watch.Restart();
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(
            image,
            new SKRect(0, 0, sourceWidth, sourceHeight),
            new SKRect(0, 0, destinationWidth, destinationHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        surface.Canvas.Flush();
        watch.Stop();
        var draw = watch.Elapsed;

        TimeSpan snapshotDuration = TimeSpan.Zero;
        if (retainSnapshot)
        {
            watch.Restart();
            using var snapshot = surface.Snapshot();
            watch.Stop();
            snapshotDuration = watch.Elapsed;
        }

        return new TransformBenchmark(
            sourceWidth,
            sourceHeight,
            destinationWidth,
            destinationHeight,
            checked((long)sourceWidth * sourceHeight * 4),
            checked((long)destinationWidth * destinationHeight * 4),
            creation,
            draw,
            snapshotDuration);
    }

    public static TransformMemoryObservation ObserveRetainedMemory(
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        SKColorSpace sourceColorSpace,
        SKColorSpace destinationColorSpace)
    {
        Collect();
        var before = Process.GetCurrentProcess().PrivateMemorySize64;
        using var bitmap = CreatePatchBitmap(
            sourceWidth,
            sourceHeight,
            sourceColorSpace,
            new ProbePixel(196, 83, 41, 255));
        using var image = SKImage.FromBitmap(bitmap);
        var withSource = Process.GetCurrentProcess().PrivateMemorySize64;
        var destinationInfo = new SKImageInfo(
            destinationWidth,
            destinationHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            destinationColorSpace);
        using var surface = SKSurface.Create(destinationInfo)
            ?? throw new InvalidOperationException("Skia could not create the memory-observation surface.");
        surface.Canvas.DrawImage(
            image,
            new SKRect(0, 0, sourceWidth, sourceHeight),
            new SKRect(0, 0, destinationWidth, destinationHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        surface.Canvas.Flush();
        using var snapshot = surface.Snapshot();
        var withDestination = Process.GetCurrentProcess().PrivateMemorySize64;
        return new TransformMemoryObservation(
            sourceWidth,
            sourceHeight,
            destinationWidth,
            destinationHeight,
            before,
            withSource,
            withDestination,
            withSource - before,
            withDestination - withSource,
            withDestination - before);
    }

    private static SKBitmap CreatePatchBitmap(
        int width,
        int height,
        SKColorSpace colorSpace,
        ProbePixel color)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        var bitmap = new SKBitmap(info);
        var premultiplied = Premultiply(color);
        var span = bitmap.GetPixelSpan();
        for (var offset = 0; offset < span.Length; offset += 4)
        {
            span[offset] = premultiplied.Blue;
            span[offset + 1] = premultiplied.Green;
            span[offset + 2] = premultiplied.Red;
            span[offset + 3] = premultiplied.Alpha;
        }

        return bitmap;
    }

    private static ProbePixel Premultiply(ProbePixel color) => color.Alpha == byte.MaxValue
        ? color
        : new ProbePixel(
            Multiply(color.Red, color.Alpha),
            Multiply(color.Green, color.Alpha),
            Multiply(color.Blue, color.Alpha),
            color.Alpha);

    private static ProbePixel Unpremultiply(byte red, byte green, byte blue, byte alpha) => alpha switch
    {
        0 => new ProbePixel(0, 0, 0, 0),
        byte.MaxValue => new ProbePixel(red, green, blue, alpha),
        _ => new ProbePixel(Divide(red, alpha), Divide(green, alpha), Divide(blue, alpha), alpha),
    };

    private static byte Multiply(byte value, byte alpha) =>
        (byte)(((value * alpha) + 127) / byte.MaxValue);

    private static byte Divide(byte value, byte alpha) =>
        (byte)Math.Min(byte.MaxValue, ((value * byte.MaxValue) + (alpha / 2)) / alpha);

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
