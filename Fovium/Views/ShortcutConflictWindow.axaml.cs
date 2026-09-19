using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Fovium.Localization;

namespace Fovium.Views;

internal sealed partial class ShortcutConflictWindow : Window
{
    public ShortcutConflictWindow(Localizer localizer, string conflictingCommand)
    {
        InitializeComponent();
        var title = localizer[UiStrings.ShortcutConflictTitle];
        Title = title;
        FindRequired<TextBlock>("TitleText").Text = title;
        FindRequired<TextBlock>("MessageText").Text = string.Format(
            System.Globalization.CultureInfo.CurrentUICulture,
            localizer[UiStrings.ShortcutConflictMessage],
            conflictingCommand);
        var cancel = FindRequired<Button>("CancelButton");
        cancel.Content = localizer[UiStrings.CommonCancel];
        cancel.Click += (_, _) => Close(false);
        var ok = FindRequired<Button>("OkButton");
        ok.Content = localizer[UiStrings.CommonOk];
        ok.Click += (_, _) => Close(true);
        var close = FindRequired<Button>("CloseButton");
        AutomationProperties.SetName(close, localizer[UiStrings.CommonClose]);
        ToolTip.SetTip(close, localizer[UiStrings.CommonClose]);
        close.Click += (_, _) => Close(false);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close(false);
            }
        };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                SettingsWindowDragOrigin.MayInitiate(e.Source as Avalonia.Visual, this))
            {
                BeginMoveDrag(e);
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Shortcut conflict control is missing: {name}.");
}