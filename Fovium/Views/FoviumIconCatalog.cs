using Avalonia.Controls;
using Avalonia.Media;

namespace Fovium.Views;

internal enum FoviumIcon
{
    Open,
    Previous,
    Next,
    Fit,
    ActualSize,
    Fullscreen,
    Settings,
    Close,
    Highlight,
    Markup,
    Hand,
    Brush,
    Eraser,
    Line,
    Rectangle,
    Ellipse,
    Arrow,
    Undo,
    Redo,
    Clear,
}

internal static class FoviumIconCatalog
{
    public static PathIcon Create(FoviumIcon icon, double size = 18) => new()
    {
        Data = StreamGeometry.Parse(GetData(icon)),
        Width = size,
        Height = size,
    };

    private static string GetData(FoviumIcon icon) => icon switch
    {
        FoviumIcon.Open => "M1,5 L6,5 L8,7 L15,7 L13,15 L2,15 Z M2,3 L8,3 L10,5 L2,5 Z",
        FoviumIcon.Previous => "M3,8 L11,2 L11,6 L15,6 L15,10 L11,10 L11,14 Z",
        FoviumIcon.Next => "M13,8 L5,2 L5,6 L1,6 L1,10 L5,10 L5,14 Z",
        FoviumIcon.Fit => "M1,1 L7,1 L7,3 L3,3 L3,7 L1,7 Z M9,1 L15,1 L15,7 L13,7 L13,3 L9,3 Z M1,9 L3,9 L3,13 L7,13 L7,15 L1,15 Z M13,9 L15,9 L15,15 L9,15 L9,13 L13,13 Z",
        FoviumIcon.ActualSize => "M2,3 L5,3 L5,13 L7,13 L7,15 L1,15 L1,13 L3,13 L3,5 L2,5 Z M9,3 L15,3 L15,15 L9,15 Z M11,5 L11,13 L13,13 L13,5 Z",
        FoviumIcon.Fullscreen => "M1,1 L7,1 L7,3 L3,3 L3,7 L1,7 Z M9,1 L15,1 L15,7 L13,7 L13,3 L9,3 Z M1,9 L3,9 L3,13 L7,13 L7,15 L1,15 Z M13,9 L15,9 L15,15 L9,15 L9,13 L13,13 Z",
        FoviumIcon.Settings => "M7,1 L9,1 L10,4 L13,3 L14,5 L12,7 L15,8 L15,10 L12,11 L14,13 L13,15 L10,14 L9,17 L7,17 L6,14 L3,15 L2,13 L4,11 L1,10 L1,8 L4,7 L2,5 L3,3 L6,4 Z M8,6 A3,3 0 1 0 8,12 A3,3 0 1 0 8,6",
        FoviumIcon.Close => "M2,3 L3,2 L8,7 L13,2 L14,3 L9,8 L14,13 L13,14 L8,9 L3,14 L2,13 L7,8 Z",
        FoviumIcon.Highlight => "M8,1 A7,7 0 1 0 8,15 A7,7 0 1 0 8,1 M8,5 A3,3 0 1 1 8,11 A3,3 0 1 1 8,5",
        FoviumIcon.Markup => "M2,12 L11,3 L14,6 L5,15 L2,15 Z M10,4 L12,2 L15,5 L13,7 Z",
        FoviumIcon.Hand => "M4,8 L4,3 L6,3 L6,7 L7,7 L7,1 L9,1 L9,7 L10,7 L10,2 L12,2 L12,8 L13,8 L13,4 L15,4 L15,10 L12,15 L6,15 L2,10 L2,8 Z",
        FoviumIcon.Brush => "M10,1 L15,6 L8,13 L3,8 Z M2,9 C5,10 7,12 7,15 C4,16 1,14 1,11 Z",
        FoviumIcon.Eraser => "M2,10 L9,3 L15,9 L9,15 L5,15 Z M4,10 L9,5 L13,9 L8,14 Z",
        FoviumIcon.Line => "M2,13 L13,2 L15,4 L4,15 Z",
        FoviumIcon.Rectangle => "M1,2 L15,2 L15,14 L1,14 Z M3,4 L3,12 L13,12 L13,4 Z",
        FoviumIcon.Ellipse => "M8,1 C13,1 16,4 16,8 C16,12 13,15 8,15 C3,15 0,12 0,8 C0,4 3,1 8,1 M8,3 C4,3 2,5 2,8 C2,11 4,13 8,13 C12,13 14,11 14,8 C14,5 12,3 8,3",
        FoviumIcon.Arrow => "M1,13 L10,4 L7,1 L15,1 L15,9 L12,6 L3,15 Z",
        FoviumIcon.Undo => "M7,3 L7,0 L1,5 L7,10 L7,7 C12,7 14,10 14,15 C16,9 13,3 7,3 Z",
        FoviumIcon.Redo => "M9,3 L9,0 L15,5 L9,10 L9,7 C4,7 2,10 2,15 C0,9 3,3 9,3 Z",
        FoviumIcon.Clear => "M3,4 L13,4 L12,15 L4,15 Z M5,1 L11,1 L12,3 L4,3 Z M6,6 L7,6 L7,13 L6,13 Z M9,6 L10,6 L10,13 L9,13 Z",
        _ => throw new ArgumentOutOfRangeException(nameof(icon)),
    };
}
