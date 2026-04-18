using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Close(true);
            e.Handled = true;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static async Task<bool> ShowAsync(Window? owner, string title, string message, string confirmLabel = "Bestätigen", bool danger = false)
    {
        var dialog = new ConfirmDialog { Title = title };

        if (dialog.FindControl<TextBlock>("TitleText") is { } titleText)
            titleText.Text = title;
        if (dialog.FindControl<TextBlock>("MessageText") is { } messageText)
            messageText.Text = message;
        if (dialog.FindControl<Button>("ConfirmButton") is { } confirmButton)
        {
            confirmButton.Content = confirmLabel;
            confirmButton.Classes.Clear();
            confirmButton.Classes.Add(danger ? "danger" : "primary");
        }

        if (owner is null)
        {
            return await dialog.ShowDialog<bool>(null!);
        }

        return await dialog.ShowDialog<bool>(owner);
    }
}
