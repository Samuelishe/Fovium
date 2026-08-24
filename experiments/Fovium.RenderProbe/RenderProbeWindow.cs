using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Fovium.RenderProbe;

internal sealed class RenderProbeWindow : Window
{
    private readonly RenderProbeControl _probe = new();
    private readonly TextBlock _diagnostics = new()
    {
        FontFamily = FontFamily.Default,
        FontSize = 12,
        Foreground = Brushes.White,
    };
    private readonly Border _diagnosticsPanel;
    private CancellationTokenSource? _loadCancellation;

    public RenderProbeWindow(string? initialPath)
    {
        Title = "Fovium R0 RenderProbe — diagnostic experiment";
        Width = 1280;
        Height = 900;
        MinWidth = 720;
        MinHeight = 480;

        _diagnosticsPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _diagnostics,
            IsHitTestVisible = false,
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(CreateToolbar());
        var viewportLayer = new Grid();
        Grid.SetRow(viewportLayer, 1);
        viewportLayer.Children.Add(_probe);
        viewportLayer.Children.Add(_diagnosticsPanel);
        root.Children.Add(viewportLayer);
        Content = root;

        _probe.DiagnosticsChanged += (_, _) => RefreshDiagnostics();
        Closed += (_, _) => DisposeOwnedResources();
        Opened += async (_, _) =>
        {
            SetPattern(PatternKind.FrequencyLab);
            if (initialPath is not null)
            {
                await LoadPathAsync(initialPath);
            }
        };
    }

    private Control CreateToolbar()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
        };

        var openButton = new Button { Content = "Open JPEG / PNG…" };
        openButton.Click += async (_, _) => await OpenFileAsync();
        toolbar.Children.Add(openButton);

        toolbar.Children.Add(CreateComboBox(
            Enum.GetValues<PatternKind>(),
            PatternKind.FrequencyLab,
            value => SetPattern(value)));

        toolbar.Children.Add(CreateComboBox(
            Enum.GetValues<RenderPath>(),
            RenderPath.DirectSkia,
            value =>
            {
                _probe.RenderPath = value;
                _probe.RefreshRendering();
            }));

        toolbar.Children.Add(CreateComboBox(
            Enum.GetValues<SamplingMode>(),
            SamplingMode.LinearMipmap,
            value =>
            {
                _probe.SamplingMode = value;
                _probe.RefreshRendering();
            }));

        var zoomLabels = new[] { "25%", "33%", "50%", "75%", "100%", "125%", "150%", "200%" };
        var zoomScales = new[] { 0.25, 1.0 / 3.0, 0.50, 0.75, 1.00, 1.25, 1.50, 2.00 };
        var zoomComboBox = new ComboBox
        {
            ItemsSource = zoomLabels,
            SelectedIndex = 4,
            MinWidth = 90,
        };
        zoomComboBox.SelectionChanged += (_, _) =>
        {
            if (zoomComboBox.SelectedIndex >= 0)
            {
                _probe.SetPhysicalScale(zoomScales[zoomComboBox.SelectedIndex]);
            }
        };
        toolbar.Children.Add(zoomComboBox);

        var fitButton = new Button { Content = "Fit" };
        fitButton.Click += (_, _) => _probe.Fit();
        toolbar.Children.Add(fitButton);

        var oneHundredButton = new Button { Content = "100% physical" };
        oneHundredButton.Click += (_, _) => _probe.SetPhotographic100();
        toolbar.Children.Add(oneHundredButton);

        var diagnosticsButton = new Button { Content = "Diagnostics" };
        diagnosticsButton.Click += (_, _) => _diagnosticsPanel.IsVisible = !_diagnosticsPanel.IsVisible;
        toolbar.Children.Add(diagnosticsButton);

        return toolbar;
    }

    private static ComboBox CreateComboBox<T>(IReadOnlyList<T> items, T selected, Action<T> changed)
        where T : struct, Enum
    {
        var comboBox = new ComboBox
        {
            ItemsSource = items,
            SelectedItem = selected,
            MinWidth = 135,
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is T value)
            {
                changed(value);
            }
        };
        return comboBox;
    }

    private void SetPattern(PatternKind pattern)
    {
        try
        {
            _probe.SetImage(ProbeImageFactory.CreatePattern(pattern));
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            _probe.SetError(exception);
        }
    }

    private async Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open local JPEG or PNG for R0 inspection",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JPEG / PNG")
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png"],
                    MimeTypes = ["image/jpeg", "image/png"],
                },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            await LoadPathAsync(path);
        }
    }

    private async Task LoadPathAsync(string path)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        try
        {
            var image = await ProbeImageFactory.LoadFileAsync(path, _loadCancellation.Token);
            _probe.SetImage(image);
        }
        catch (OperationCanceledException)
        {
            // A newer explicit load owns the probe now.
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _probe.SetError(exception);
        }
    }

    private void RefreshDiagnostics() => _diagnostics.Text = _probe.GetDiagnostics();

    private void DisposeOwnedResources()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _probe.DisposeImage();
    }
}
