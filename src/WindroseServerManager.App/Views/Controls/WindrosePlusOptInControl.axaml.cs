using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Controls;

public partial class WindrosePlusOptInControl : UserControl
{
    private const string GitHubUrl = "https://github.com/HumanGenome/WindrosePlus";

    public WindrosePlusOptInControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnGitHubLinkClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            // URL-Start fehlgeschlagen — User kann Link manuell öffnen.
        }
    }

    private void OnTogglePasswordVisibility(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("RconPasswordBox");
        if (box is null) return;
        box.PasswordChar = box.PasswordChar == '\0' ? '•' : '\0';
    }

    private async void OnCopyPassword(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not IWindrosePlusOptInContext ctx) return;
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard is null) return;
            await top.Clipboard.SetTextAsync(ctx.RconPassword ?? string.Empty);
        }
        catch
        {
            // Clipboard access failed — silent, user can retry.
        }
    }

    private void OnRegeneratePassword(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IWindrosePlusOptInContext ctx)
            ctx.RegeneratePassword();
    }

    private void OnSteamIdLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IWindrosePlusOptInContext ctx)
            ctx.ValidateSteamId();
    }
}
