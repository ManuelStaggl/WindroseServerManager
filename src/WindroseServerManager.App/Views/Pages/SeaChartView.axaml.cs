using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Pages;

public partial class SeaChartView : UserControl
{
    private NativeWebView? _webView;
    private bool _loginHandled;

    public SeaChartView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _webView = this.FindControl<NativeWebView>("LiveMapWebView");
        if (_webView is not null)
            _webView.NavigationCompleted += OnNavigationCompleted;
        (DataContext as SeaChartViewModel)?.Start();
        _loginHandled = false;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_webView is not null)
            _webView.NavigationCompleted -= OnNavigationCompleted;
        (DataContext as SeaChartViewModel)?.Stop();
    }

    private void OnNavigationCompleted(object? sender, EventArgs e)
    {
        if (_webView is null || _loginHandled) return;
        var vm = DataContext as SeaChartViewModel;
        if (vm is null) return;
        _ = HandleLoginIfNeededAsync(vm);
    }

    private async Task HandleLoginIfNeededAsync(SeaChartViewModel vm)
    {
        if (_webView is null || _loginHandled) return;

        // Ask the page its current URL — most reliable since Avalonia's event args type is internal.
        string currentUrl;
        try
        {
            currentUrl = await _webView.InvokeScript("location.href") ?? string.Empty;
        }
        catch
        {
            return;
        }

        if (!currentUrl.Contains("/login", StringComparison.OrdinalIgnoreCase))
            return;

        _loginHandled = true;

        // Login via HTTP to get the session cookie, then inject it into the WebView.
        try
        {
            var session = await vm.GetSessionCookieAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(session)) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_webView is null) return;
                var escaped = session.Replace("'", "\\'");
                await _webView.InvokeScript($"document.cookie = 'wp_session={escaped}; path=/;'");
                var livemap = vm.GetLivemapUri();
                if (livemap is not null)
                    _webView.Navigate(livemap);
            });
        }
        catch { /* silently fail — user can enter password manually */ }
    }
}
