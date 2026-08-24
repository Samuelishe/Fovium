using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Fovium.Application;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Navigation;
using Fovium.Viewer;
using ViewerNavigationDirection = Fovium.Navigation.NavigationDirection;

namespace Fovium.Views;

internal sealed partial class ViewerWindow : Window
{
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(1.75);

    private readonly ActivationService _activation;
    private readonly ViewerSession<DecodedImage> _session;
    private readonly Localizer _localizer;
    private readonly IReadOnlyList<string> _startupPaths;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Cursor _visibleCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private readonly ContextMenu _contextMenu;
    private readonly MenuItem _previousMenuItem;
    private readonly MenuItem _nextMenuItem;
    private bool _contextMenuOpen;
    private bool _closed;
    private WindowState _windowStateBeforeFullscreen = WindowState.Maximized;

    public ViewerWindow(
        ActivationService activation,
        ViewerSession<DecodedImage> session,
        Localizer localizer,
        IReadOnlyList<string> startupPaths)
    {
        _activation = activation;
        _session = session;
        _localizer = localizer;
        _startupPaths = startupPaths;

        InitializeComponent();
        _previousMenuItem = CreateMenuItem(
            UiStrings.MenuPrevious,
            async () => await NavigateAsync(ViewerNavigationDirection.Previous));
        _nextMenuItem = CreateMenuItem(
            UiStrings.MenuNext,
            async () => await NavigateAsync(ViewerNavigationDirection.Next));
        _contextMenu = CreateContextMenu();
        PhotoViewport.ContextMenu = _contextMenu;

        _cursorTimer = new DispatcherTimer { Interval = CursorHideDelay };
        _cursorTimer.Tick += OnCursorTimerTick;
        PhotoViewport.PointerActivity += OnPointerActivity;
        Opened += OnOpened;
        Closed += OnClosed;
        KeyDown += OnWindowKeyDown;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            PhotoViewport.Focus();
            RestartCursorTimer();
            if (_startupPaths.Count == 0)
            {
                var selected = await PickFilesAsync();
                if (selected.Count == 0)
                {
                    Close();
                    return;
                }

                await OpenPathsAsync(selected);
                return;
            }

            await OpenPathsAsync(_startupPaths);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsRecoverableBoundaryException(exception))
        {
            ShowBoundaryError();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _cursorTimer.Stop();
        _lifetimeCancellation.Cancel();
        PhotoViewport.ClearImage();
        _session.Dispose();
        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Left)
            {
                e.Handled = true;
                await NavigateAsync(ViewerNavigationDirection.Previous);
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
                await NavigateAsync(ViewerNavigationDirection.Next);
            }
            else if (e.Key == Key.F11)
            {
                e.Handled = true;
                ToggleFullscreen();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (WindowState == WindowState.FullScreen)
                {
                    LeaveFullscreen();
                }
                else
                {
                    Close();
                }
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                await OpenFromPickerAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsRecoverableBoundaryException(exception))
        {
            ShowBoundaryError();
        }
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                CreateMenuItem(UiStrings.MenuOpen, OpenFromPickerAsync),
                _previousMenuItem,
                _nextMenuItem,
                new Separator(),
                CreateMenuItem(UiStrings.MenuFit, () =>
                {
                    PhotoViewport.Fit();
                    return Task.CompletedTask;
                }),
                CreateMenuItem(UiStrings.MenuActualSize, () =>
                {
                    PhotoViewport.SetPhotographic100AtCenter();
                    return Task.CompletedTask;
                }),
                new Separator(),
                CreateMenuItem(UiStrings.MenuFullscreen, () =>
                {
                    ToggleFullscreen();
                    return Task.CompletedTask;
                }),
                CreateMenuItem(UiStrings.MenuClose, () =>
                {
                    Close();
                    return Task.CompletedTask;
                }),
            },
        };
        menu.Opening += (_, _) =>
        {
            _contextMenuOpen = true;
            ShowCursor();
            _cursorTimer.Stop();
            _previousMenuItem.IsEnabled = _session.CanNavigate(ViewerNavigationDirection.Previous);
            _nextMenuItem.IsEnabled = _session.CanNavigate(ViewerNavigationDirection.Next);
        };
        menu.Closing += (_, _) =>
        {
            _contextMenuOpen = false;
            RestartCursorTimer();
        };
        return menu;
    }

    private MenuItem CreateMenuItem(string key, Func<Task> action)
    {
        var item = new MenuItem { Header = _localizer[key] };
        item.Click += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (IsRecoverableBoundaryException(exception))
            {
                ShowBoundaryError();
            }
        };
        return item;
    }

    private async Task OpenFromPickerAsync()
    {
        if (_closed)
        {
            return;
        }

        var selected = await PickFilesAsync();
        if (selected.Count > 0)
        {
            await OpenPathsAsync(selected);
        }
    }

    private async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        ShowCursor();
        _cursorTimer.Stop();
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _localizer[UiStrings.PickerTitle],
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(_localizer[UiStrings.PickerImageType])
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png"],
                        MimeTypes = ["image/jpeg", "image/png"],
                    },
                ],
            });

            return files
                .Select(file => file.TryGetLocalPath())
                .Where(path => path is not null)
                .Cast<string>()
                .ToArray();
        }
        finally
        {
            RestartCursorTimer();
        }
    }

    private async Task OpenPathsAsync(IReadOnlyList<string> paths)
    {
        var plan = ActivationPlan.Create(paths);
        var sequence = await _activation.ResolveAsync(plan, _lifetimeCancellation.Token);
        if (sequence is null)
        {
            return;
        }

        var result = await _session.OpenAsync(sequence, _lifetimeCancellation.Token);
        ApplySelection(result, preserveView: false, showFailure: true);
    }

    private async Task NavigateAsync(ViewerNavigationDirection direction)
    {
        var result = await _session.NavigateAsync(direction, _lifetimeCancellation.Token);
        ApplySelection(result, preserveView: true, showFailure: false);
    }

    private void ApplySelection(
        SelectionResult<DecodedImage> result,
        bool preserveView,
        bool showFailure)
    {
        if (_closed)
        {
            result.Image?.Dispose();
            return;
        }

        if (result.Status == SelectionStatus.Published && result.Image is not null)
        {
            ErrorSurface.IsVisible = false;
            PhotoViewport.SetImage(result.Image, preserveView);
            return;
        }

        result.Image?.Dispose();
        if (!showFailure || result.Status != SelectionStatus.Failed || result.Error is null)
        {
            return;
        }

        PhotoViewport.ClearImage();
        ErrorText.Text = LocalizeError(result.Error.Kind);
        ErrorSurface.IsVisible = true;
    }

    private string LocalizeError(ImageLoadErrorKind kind) =>
        _localizer[kind switch
        {
            ImageLoadErrorKind.Missing => UiStrings.ErrorMissing,
            ImageLoadErrorKind.Unsupported => UiStrings.ErrorUnsupported,
            ImageLoadErrorKind.Corrupt => UiStrings.ErrorCorrupt,
            ImageLoadErrorKind.ResourceLimit => UiStrings.ErrorResourceLimit,
            ImageLoadErrorKind.DecodeFailed => UiStrings.ErrorDecodeFailed,
            _ => UiStrings.ErrorDecodeFailed,
        }];

    private void ShowBoundaryError()
    {
        if (_closed)
        {
            return;
        }

        PhotoViewport.ClearImage();
        ErrorText.Text = _localizer[UiStrings.ErrorDecodeFailed];
        ErrorSurface.IsVisible = true;
    }

    private static bool IsRecoverableBoundaryException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException;

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            LeaveFullscreen();
            return;
        }

        _windowStateBeforeFullscreen = WindowState;
        WindowState = WindowState.FullScreen;
    }

    private void LeaveFullscreen() => WindowState = _windowStateBeforeFullscreen;

    private void OnPointerActivity(object? sender, EventArgs e)
    {
        ShowCursor();
        RestartCursorTimer();
    }

    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        _cursorTimer.Stop();
        if (!_contextMenuOpen)
        {
            PhotoViewport.Cursor = _hiddenCursor;
        }
    }

    private void ShowCursor() => PhotoViewport.Cursor = _visibleCursor;

    private void RestartCursorTimer()
    {
        if (_lifetimeCancellation.IsCancellationRequested || _contextMenuOpen)
        {
            return;
        }

        _cursorTimer.Stop();
        _cursorTimer.Start();
    }
}
