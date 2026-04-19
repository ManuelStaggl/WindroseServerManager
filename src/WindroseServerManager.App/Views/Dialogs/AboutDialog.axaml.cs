using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using WindroseServerManager.App.Services;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        if (this.FindControl<TextBlock>("VersionText") is { } text)
            text.Text = Loc.Format("Settings.About.VersionFormat", version);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // URL-Start fehlgeschlagen — ignorieren, User kann Link manuell öffnen.
            }
        }
    }

    private async void OnShowWindrosePlusLicenseClick(object? sender, RoutedEventArgs e)
    {
        var textBlock = this.FindControl<TextBlock>("WindrosePlusLicenseText");
        var box = this.FindControl<Border>("WindrosePlusLicenseBox");
        if (textBlock is null || box is null) return;

        try
        {
            if (textBlock.Text is { Length: > 0 })
            {
                box.IsVisible = !box.IsVisible;
                return;
            }
            var uri = new Uri("avares://WindroseServerManager/Resources/Licenses/WindrosePlus-LICENSE.txt");
            await using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            textBlock.Text = await reader.ReadToEndAsync();
            box.IsVisible = true;
        }
        catch (Exception ex)
        {
            textBlock.Text = "Failed to load license: " + ex.Message;
            box.IsVisible = true;
        }
    }

    public static Task ShowAsync(Window owner)
    {
        var dialog = new AboutDialog();
        return dialog.ShowDialog(owner);
    }
}
