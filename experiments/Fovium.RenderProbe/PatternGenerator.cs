using SkiaSharp;

namespace Fovium.RenderProbe;

internal static class PatternGenerator
{
    private const int Width = 1200;
    private const int Height = 800;

    public static SKBitmap Create(PatternKind kind)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(
            Width,
            Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            colorSpace);
        var bitmap = new SKBitmap(info);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Black);

            switch (kind)
            {
                case PatternKind.PixelGrid:
                    DrawPixelGrid(canvas);
                    break;
                case PatternKind.FrequencyLab:
                    DrawFrequencyLab(canvas);
                    break;
                case PatternKind.AlphaEdges:
                    DrawAlphaEdges(canvas);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            canvas.Flush();
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void DrawPixelGrid(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = false };
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var checker = ((x / 8) + (y / 8)) % 2 == 0;
                paint.Color = checker ? new SKColor(238, 238, 238) : new SKColor(24, 24, 24);
                canvas.DrawPoint(x, y, paint);
            }
        }

        paint.Color = SKColors.Red;
        paint.StrokeWidth = 1;
        for (var x = 100; x < Width; x += 64)
        {
            canvas.DrawLine(x, 0, x, Height, paint);
        }

        paint.Color = SKColors.Lime;
        for (var y = 100; y < Height; y += 64)
        {
            canvas.DrawLine(0, y, Width, y, paint);
        }

        paint.Color = SKColors.Cyan;
        canvas.DrawLine(0, 0, Width - 1, Height - 1, paint);
        canvas.DrawLine(Width - 1, 0, 0, Height - 1, paint);
    }

    private static void DrawFrequencyLab(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = false };
        using var cutoutPaint = new SKPaint { IsAntialias = false, Color = SKColors.Black };
        var tileSizes = new[] { 1, 2, 3, 4, 6, 8, 12, 16 };
        for (var band = 0; band < tileSizes.Length; band++)
        {
            var tile = tileSizes[band];
            var left = band * (Width / tileSizes.Length);
            var right = (band + 1) * (Width / tileSizes.Length);
            for (var y = 0; y < 300; y++)
            {
                for (var x = left; x < right; x++)
                {
                    paint.Color = ((x / tile) + (y / tile)) % 2 == 0 ? SKColors.White : SKColors.Black;
                    canvas.DrawPoint(x, y, paint);
                }
            }
        }

        var centerX = Width / 2.0;
        var centerY = 545.0;
        for (var y = 300; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var dx = (x - centerX) / Width;
                var dy = (y - centerY) / Height;
                var value = Math.Sin(1800 * (dx * dx + dy * dy));
                var gray = (byte)Math.Clamp((value + 1) * 127.5, 0, 255);
                paint.Color = new SKColor(gray, gray, gray);
                canvas.DrawPoint(x, y, paint);
            }
        }

        paint.Color = SKColors.Yellow;
        paint.StrokeWidth = 1;
        for (var line = 0; line < 14; line++)
        {
            var y = 340 + line * 24;
            canvas.DrawLine(24, y, 330, y + (line % 3), paint);
            for (var glyph = 0; glyph < 18; glyph++)
            {
                var x = 360 + glyph * 18;
                canvas.DrawRect(x, y, 10, 14, paint);
                canvas.DrawRect(x + 3, y + 3, 4, 8, cutoutPaint);
            }
        }
    }

    private static void DrawAlphaEdges(SKCanvas canvas)
    {
        canvas.Clear(new SKColor(36, 36, 36));
        using var grid = new SKPaint { IsAntialias = false };
        for (var y = 0; y < Height; y += 32)
        {
            for (var x = 0; x < Width; x += 32)
            {
                grid.Color = ((x + y) / 32) % 2 == 0
                    ? new SKColor(80, 80, 80)
                    : new SKColor(150, 150, 150);
                canvas.DrawRect(x, y, 32, 32, grid);
            }
        }

        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(255, 80, 20, 128) };
        canvas.DrawCircle(330, 400, 240, paint);
        paint.Color = new SKColor(20, 140, 255, 96);
        canvas.DrawRect(470, 150, 520, 500, paint);
        paint.IsAntialias = false;
        paint.Color = new SKColor(255, 255, 255, 128);
        canvas.DrawRect(1000, 0, 1, Height, paint);
    }
}
