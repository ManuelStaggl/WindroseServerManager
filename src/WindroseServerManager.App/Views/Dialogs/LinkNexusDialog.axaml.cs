using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class LinkNexusDialog : Window
{
    private string _expectedDomain = "windrose";

    public LinkNexusDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close((int?)null); e.Handled = true; }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnConfirmClick(sender, new RoutedEventArgs()); e.Handled = true; }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close((int?)null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("UrlInput")?.Text ?? string.Empty;

        if (!NexusUrlParser.TryParse(input, _expectedDomain, out var modId, out var reason))
        {
            ShowError(reason ?? Loc.Get("Dialog.LinkNexus.InvalidUrl"));
            return;
        }

        Close((int?)modId);
    }

    private void ShowError(string message)
    {
        if (this.FindControl<Border>("ErrorPanel") is { } panel) panel.IsVisible = true;
        if (this.FindControl<TextBlock>("ErrorText") is { } text) text.Text = message;
    }

    public static async Task<int?> ShowAsync(Window owner, string expectedDomain, string modDisplayName)
    {
        var dlg = new LinkNexusDialog { _expectedDomain = expectedDomain };
        if (dlg.FindControl<TextBlock>("ModNameLabel") is { } lbl)
            lbl.Text = Loc.Format("Dialog.LinkNexus.ForModFormat", modDisplayName);
        return await dlg.ShowDialog<int?>(owner);
    }
}
