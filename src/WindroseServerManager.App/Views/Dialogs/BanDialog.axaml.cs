using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WindroseServerManager.App.Views.Dialogs;

public sealed record BanDialogResult(int? Minutes); // null = permanent, int = timed

public partial class BanDialog : Window
{
    public BanDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
                e.Handled = true;
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var timed   = this.FindControl<RadioButton>("TimedRadio");
        var minutes = this.FindControl<NumericUpDown>("MinutesInput");
        if (timed?.IsChecked == true && minutes?.Value is { } v)
            Close(new BanDialogResult((int)v));
        else
            Close(new BanDialogResult(null));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    public static async Task<BanDialogResult?> ShowAsync(Window? owner, string title, string message)
    {
        var dlg = new BanDialog { Title = title };
        if (dlg.FindControl<TextBlock>("TitleText")   is { } t) t.Text = title;
        if (dlg.FindControl<TextBlock>("MessageText") is { } m) m.Text = message;
        return owner is null
            ? await dlg.ShowDialog<BanDialogResult?>(null!)
            : await dlg.ShowDialog<BanDialogResult?>(owner);
    }
}
