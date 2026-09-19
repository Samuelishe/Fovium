using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Fovium.Views;

internal sealed class ColorSwatchButton : Button
{
    public static readonly StyledProperty<IBrush?> SwatchBrushProperty =
        AvaloniaProperty.Register<ColorSwatchButton, IBrush?>(nameof(SwatchBrush));

    public IBrush? SwatchBrush
    {
        get => GetValue(SwatchBrushProperty);
        set => SetValue(SwatchBrushProperty, value);
    }
}