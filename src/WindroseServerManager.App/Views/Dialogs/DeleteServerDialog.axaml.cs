using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class DeleteServerDialog : Window
{
    private CheckBox? _deleteFilesCheck;

    public bool DeleteFiles =>
        (_deleteFilesCheck?.IsChecked ?? false) == true;

    public DeleteServerDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _deleteFilesCheck = this.FindControl<CheckBox>("DeleteFilesCheck");
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(false); e.Handled = true; }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static async Task<(bool Confirmed, bool DeleteFiles)> ShowAsync(Window? owner, string serverName)
    {
        var dialog = new DeleteServerDialog();
        if (dialog.FindControl<TextBlock>("ServerNameText") is { } txt)
            txt.Text = serverName;

        var confirmed = owner is null
            ? await dialog.ShowDialog<bool>(null!)
            : await dialog.ShowDialog<bool>(owner);

        return (confirmed, confirmed && dialog.DeleteFiles);
    }
}
