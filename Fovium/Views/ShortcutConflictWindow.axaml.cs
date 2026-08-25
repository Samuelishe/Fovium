using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Fovium.Localization;

namespace Fovium.Views;

internal sealed partial class ShortcutConflictWindow : Window
{
    public ShortcutConflictWindow(Localizer localizer, string conflictingCommand)
    {
        InitializeComponent();
        Title = localizer[UiStrings.ShortcutConflictTitle];
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
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Shortcut conflict control is missing: {name}.");
}
