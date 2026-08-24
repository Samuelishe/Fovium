namespace Fovium.RenderProbe;

internal enum RenderPath
{
    AvaloniaDrawingContext,
    DirectSkia,
}

internal enum SamplingMode
{
    Nearest,
    Linear,
    LinearMipmap,
    Mitchell,
    CatmullRom,
}

internal enum PatternKind
{
    PixelGrid,
    FrequencyLab,
    AlphaEdges,
}
