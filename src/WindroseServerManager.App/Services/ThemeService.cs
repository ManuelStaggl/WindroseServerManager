using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.App.Services;

public sealed class ThemeService : IThemeService
{
    public void Apply(ThemeMode mode)
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        // Avalonia re-resolved DynamicResource bindings automatisch beim ThemeVariant-Wechsel,
        // aber manche Controls mit custom-styles aktualisieren erst nach InvalidateVisual.
        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.InvalidateVisual();
        }
    }
}
