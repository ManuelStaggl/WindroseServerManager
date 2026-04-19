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
        try
        {
            if (WindrosePlusLicenseText.Text is { Length: > 0 })
            {
                WindrosePlusLicenseBox.IsVisible = !WindrosePlusLicenseBox.IsVisible;
                return;
            }
            var uri = new Uri("avares://WindroseServerManager/Resources/Licenses/WindrosePlus-LICENSE.txt");
            await using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            WindrosePlusLicenseText.Text = await reader.ReadToEndAsync();
            WindrosePlusLicenseBox.IsVisible = true;
        }
        catch (Exception ex)
        {
            // Fallback: zeige Fehlermeldung statt den Dialog zu crashen.
            WindrosePlusLicenseText.Text = "Failed to load license: " + ex.Message;
            WindrosePlusLicenseBox.IsVisible = true;
        }
    }

    public static Task ShowAsync(Window owner)
    {
        var dialog = new AboutDialog();
        return dialog.ShowDialog(owner);
    }
}
