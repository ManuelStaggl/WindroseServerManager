using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class LinkNexusDialog : Window
{
    private INexusClient? _nexus;
    private string _expectedDomain = "windrose";
    private NexusModInfo? _fetched;

    public LinkNexusDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close((NexusModInfo?)null); e.Handled = true; }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnFetchClick(sender, new RoutedEventArgs()); e.Handled = true; }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close((NexusModInfo?)null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(_fetched);

    private async void OnFetchClick(object? sender, RoutedEventArgs e)
    {
        if (_nexus is null) return;
        var input = this.FindControl<TextBox>("UrlInput")?.Text ?? string.Empty;

        if (!NexusUrlParser.TryParse(input, _expectedDomain, out var modId, out var reason))
        {
            ShowStatus(error: true, Loc.Get("Dialog.LinkNexus.InvalidUrl"), reason ?? string.Empty);
            SetConfirmable(false);
            return;
        }

        SetButtonsBusy(true);
        ShowStatus(error: false, Loc.Get("Dialog.LinkNexus.Fetching"), $"Mod #{modId}");

        try
        {
            _fetched = await _nexus.GetModAsync(modId);
            if (_fetched is null)
            {
                ShowStatus(error: true, Loc.Get("Dialog.LinkNexus.NotFound"), $"Mod #{modId}");
                SetConfirmable(false);
                return;
            }
            ShowStatus(error: false,
                $"{_fetched.Name}  ·  v{_fetched.Version}",
                _fetched.Summary);
            SetConfirmable(true);
        }
        catch (Exception ex)
        {
            ShowStatus(error: true, Loc.Get("Dialog.LinkNexus.FetchFailed"), ErrorMessageHelper.FriendlyMessage(ex));
            SetConfirmable(false);
        }
        finally
        {
            SetButtonsBusy(false);
        }
    }

    private void SetConfirmable(bool canConfirm)
    {
        if (this.FindControl<Button>("ConfirmButton") is { } btn) btn.IsEnabled = canConfirm;
    }

    private void SetButtonsBusy(bool busy)
    {
        if (this.FindControl<Button>("FetchButton") is { } fetch) fetch.IsEnabled = !busy;
    }

    private void ShowStatus(bool error, string headline, string detail)
    {
        if (this.FindControl<Border>("StatusPanel") is { } panel)
        {
            panel.IsVisible = true;
            var keyOk = error ? "BrandErrorBrush" : "BrandSuccessBrush";
            if (Application.Current?.Resources.TryGetResource(keyOk, Application.Current.ActualThemeVariant, out var brush) == true
                && brush is IBrush b)
            {
                panel.Background = b;
            }
        }
        if (this.FindControl<TextBlock>("StatusHeadline") is { } head)
        {
            head.Text = headline;
            head.Foreground = Brushes.White;
        }
        if (this.FindControl<TextBlock>("StatusDetail") is { } det)
        {
            det.Text = detail;
            det.Foreground = Brushes.White;
        }
    }

    public static async Task<NexusModInfo?> ShowAsync(Window owner, INexusClient nexus, string expectedDomain, string modDisplayName)
    {
        var dlg = new LinkNexusDialog
        {
            _nexus = nexus,
            _expectedDomain = expectedDomain,
        };
        if (dlg.FindControl<TextBlock>("ModNameLabel") is { } lbl)
            lbl.Text = Loc.Format("Dialog.LinkNexus.ForModFormat", modDisplayName);
        return await dlg.ShowDialog<NexusModInfo?>(owner);
    }
}
