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
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Stage;
using Fovium.Viewer;
using ViewerNavigationDirection = Fovium.Navigation.NavigationDirection;

namespace Fovium.Views;

internal sealed partial class ViewerWindow : Window
{
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(1.75);

    private readonly ActivationService _activation;
    private readonly ViewerSession<DecodedImage> _session;
    private readonly Localizer _localizer;
    private readonly SettingsService _settings;
    private readonly AmbientStageCoordinator _stageCoordinator;
    private readonly IReadOnlyList<string> _startupPaths;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _cursorTimer;
    private readonly Cursor _visibleCursor = new(StandardCursorType.Arrow);
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private readonly ContextMenu _contextMenu;
    private readonly MenuItem _previousMenuItem;
    private readonly MenuItem _nextMenuItem;
    private readonly IReadOnlyDictionary<StageMode, MenuItem> _stageMenuItems;
    private bool _contextMenuOpen;
    private bool _closed;
    private bool _shutdownCompleted;
    private bool _shutdownStarted;
    private SettingsWindow? _settingsWindow;
    private WindowState _windowStateBeforeFullscreen = WindowState.Maximized;

    public ViewerWindow(
        ActivationService activation,
        ViewerSession<DecodedImage> session,
        Localizer localizer,
        SettingsService settings,
        IReadOnlyList<string> startupPaths)
    {
        _activation = activation;
        _session = session;
        _localizer = localizer;
        _settings = settings;
        _startupPaths = startupPaths;
        _stageCoordinator = new AmbientStageCoordinator(
            new AmbientImageRepository(session),
            new AmbientStagePreparer(),
            settings.Current.StageMode);

        InitializeComponent();
        _previousMenuItem = CreateMenuItem(
            UiStrings.MenuPrevious,
            async () => await NavigateAsync(ViewerNavigationDirection.Previous));
        _nextMenuItem = CreateMenuItem(
            UiStrings.MenuNext,
            async () => await NavigateAsync(ViewerNavigationDirection.Next));
        _stageMenuItems = CreateStageMenuItems();
        _contextMenu = CreateContextMenu();
        PhotoViewport.ContextMenu = _contextMenu;

        _cursorTimer = new DispatcherTimer { Interval = CursorHideDelay };
        _cursorTimer.Tick += OnCursorTimerTick;
        PhotoViewport.PointerActivity += OnPointerActivity;
        _stageCoordinator.PresentationChanged += OnStagePresentationChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
        KeyDown += OnWindowKeyDown;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            PhotoViewport.Focus();
            RestartCursorTimer();
            await _settings.InitializeAsync(_lifetimeCancellation.Token);
            ApplyStageMode(_settings.Current.StageMode);
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

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        _closed = true;
        _cursorTimer.Stop();
        _settingsWindow?.Close();
        _lifetimeCancellation.Cancel();
        PhotoViewport.ClearImage();
        try
        {
            await _settings.FlushAsync();
            await _stageCoordinator.DisposeAsync();
            await _session.DisposeAsync();
        }
        finally
        {
            _shutdownCompleted = true;
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _lifetimeCancellation.Dispose();
        _visibleCursor.Dispose();
        _hiddenCursor.Dispose();
        _settings.SettingsChanged -= OnSettingsChanged;
        _stageCoordinator.PresentationChanged -= OnStagePresentationChanged;
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
            else if (e.Key == Key.OemComma && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                ShowSettings();
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
                new MenuItem
                {
                    Header = _localizer[UiStrings.MenuStage],
                    ItemsSource = _stageMenuItems.Values,
                },
                new Separator(),
                CreateMenuItem(UiStrings.MenuFullscreen, () =>
                {
                    ToggleFullscreen();
                    return Task.CompletedTask;
                }),
                new Separator(),
                CreateMenuItem(UiStrings.MenuSettings, () =>
                {
                    ShowSettings();
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
            foreach (var (mode, item) in _stageMenuItems)
            {
                item.IsChecked = _settings.Current.StageMode == mode;
            }
        };
        menu.Closing += (_, _) =>
        {
            _contextMenuOpen = false;
            RestartCursorTimer();
        };
        return menu;
    }

    private IReadOnlyDictionary<StageMode, MenuItem> CreateStageMenuItems()
    {
        var labels = new Dictionary<StageMode, string>
        {
            [StageMode.Black] = UiStrings.StageBlack,
            [StageMode.Neutral] = UiStrings.StageNeutral,
            [StageMode.Ambient] = UiStrings.StageAmbient,
            [StageMode.AmbientMatte] = UiStrings.StageAmbientMatte,
        };
        return labels.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var item = new MenuItem
                {
                    Header = _localizer[pair.Value],
                    ToggleType = MenuItemToggleType.Radio,
                };
                item.Click += async (_, _) =>
                {
                    try
                    {
                        await _settings.SetStageModeAsync(pair.Key, _lifetimeCancellation.Token);
                    }
                    catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
                    {
                    }
                };
                return item;
            });
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
        ApplySelection(result, ImageChangeViewPolicyResolver.ForNewSequence(), showFailure: true);
    }

    private async Task NavigateAsync(ViewerNavigationDirection direction)
    {
        var result = await _session.NavigateAsync(direction, _lifetimeCancellation.Token);
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            _settings.Current.ImageChangeViewPolicy,
            PhotoViewport.CaptureViewTransfer());
        ApplySelection(result, transfer, showFailure: false);
    }

    private void ApplySelection(
        SelectionResult<DecodedImage> result,
        ViewTransfer transfer,
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
            var path = result.Path
                ?? throw new InvalidOperationException("Published selection has no source path.");
            var identity = result.Image.Value.Identity;
            PhotoViewport.SetImage(result.Image, transfer);
            _stageCoordinator.SelectImage(path, identity);
            return;
        }

        result.Image?.Dispose();
        if (!showFailure || result.Status != SelectionStatus.Failed || result.Error is null)
        {
            return;
        }

        PhotoViewport.ClearImage();
        _stageCoordinator.ClearImage();
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
        _stageCoordinator.ClearImage();
        ErrorText.Text = _localizer[UiStrings.ErrorDecodeFailed];
        ErrorSurface.IsVisible = true;
    }

    private void ShowSettings()
    {
        ShowCursor();
        _cursorTimer.Stop();
        if (_settingsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new SettingsWindow(_settings, _localizer);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            RestartCursorTimer();
        };
        window.Show(this);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyStageMode(e.Settings.StageMode);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplyStageMode(e.Settings.StageMode));
        }
    }

    private void ApplyStageMode(StageMode mode)
    {
        if (_closed)
        {
            return;
        }

        _stageCoordinator.SetMode(mode);
    }

    private void OnStagePresentationChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyStagePresentation();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyStagePresentation);
        }
    }

    private void ApplyStagePresentation()
    {
        if (_closed)
        {
            return;
        }

        using var presentation = _stageCoordinator.AcquirePresentation();
        PhotoViewport.SetStage(presentation.Mode, presentation.TakeAmbient());
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
