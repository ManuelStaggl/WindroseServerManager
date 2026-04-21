using System;
using System.Diagnostics;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class SeaChartViewModel : ViewModelBase, IDisposable
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private Uri? _livemapUrl;
    [ObservableProperty] private string? _errorMessage;

    public bool HasLivemap => LivemapUrl is not null;

    public string RconPassword
    {
        get
        {
            var dir = _settings.ActiveServerDir;
            if (string.IsNullOrWhiteSpace(dir)) return string.Empty;
            var config = _api.ReadConfig(dir);
            if (config?.Rcon.TryGetValue("password", out var pw) == true && pw is not null)
            {
                var s = pw is System.Text.Json.JsonElement el ? el.GetString() : pw as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return _settings.Current.WindrosePlusRconPasswordByServer
                .GetValueOrDefault(dir, string.Empty);
        }
    }

    partial void OnLivemapUrlChanged(Uri? value) => OnPropertyChanged(nameof(HasLivemap));

    public SeaChartViewModel(IWindrosePlusApiService api, IAppSettingsService settings)
    {
        _api = api;
        _settings = settings;
    }

    public void Start() => RefreshUrl();
    public void Stop() { _cts?.Cancel(); }
    public void UpdateCanvasSize(double width, double height) { /* reserved for future canvas rendering */ }

    /// <summary>
    /// Builds a self-submitting HTML form that logs into the WindrosePlus dashboard
    /// so the WebView gets the session cookie without showing a login page to the user.
    /// Returns null if no server/password is configured.
    /// </summary>
    public Task<string?> GetSessionCookieAsync(CancellationToken ct = default)
    {
        var dir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(dir)) return Task.FromResult<string?>(null);
        return _api.GetSessionCookieAsync(dir, ct);
    }

    public string? BuildAutoLoginHtml()
    {
        var dir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(dir)) return null;

        var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(dir, 0);
        if (port <= 0) return null;

        var pw = RconPassword;
        if (string.IsNullOrEmpty(pw)) return null;

        // HTML-encode the password to prevent injection in the form value
        var encodedPw = SecurityElement.Escape(pw) ?? string.Empty;

        return $"""
            <!DOCTYPE html>
            <html>
            <body onload="document.getElementById('lf').submit()">
              <form id="lf" action="http://localhost:{port}/login" method="POST">
                <input type="hidden" name="password" value="{encodedPw}">
              </form>
            </body>
            </html>
            """;
    }

    public Uri? GetLivemapUri()
    {
        var dir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(dir)) return null;
        var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(dir, 0);
        return port > 0 ? new Uri($"http://localhost:{port}/livemap") : null;
    }

    private void RefreshUrl()
    {
        var serverDir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(serverDir)) { LivemapUrl = null; return; }
        var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(serverDir, 0);
        LivemapUrl = port > 0 ? new Uri($"http://localhost:{port}/livemap") : null;
    }

    [RelayCommand]
    private void Reload() => RefreshUrl();

    [RelayCommand]
    private void OpenLivemap()
    {
        if (LivemapUrl is null) return;
        try { Process.Start(new ProcessStartInfo(LivemapUrl.ToString()) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warning(ex, "Failed to open livemap in browser"); }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
