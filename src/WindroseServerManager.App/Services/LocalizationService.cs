using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

namespace WindroseServerManager.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const string DeUri = "avares://WindroseServerManager/Resources/Strings/Strings.de.axaml";
    private const string EnUri = "avares://WindroseServerManager/Resources/Strings/Strings.en.axaml";

    private ResourceDictionary? _activeDict;
    private string _currentSetting = "auto";
    private string _currentLanguage = "de";

    public string CurrentLanguage => _currentLanguage;
    public string CurrentSetting => _currentSetting;
    public event Action? LanguageChanged;

    public string this[string key]
    {
        get
        {
            var app = Application.Current;
            if (app is null) return key;
            if (app.Resources.TryGetResource(key, app.ActualThemeVariant, out var val) && val is string s)
                return s;
            return key;
        }
    }

    public void Initialize(string setting) => Apply(setting, fireEvent: false);

    public void SetLanguage(string setting)
    {
        if (string.Equals(setting, _currentSetting, StringComparison.OrdinalIgnoreCase)
            && _activeDict is not null)
            return;
        Apply(setting, fireEvent: true);
    }

    private void Apply(string setting, bool fireEvent)
    {
        _currentSetting = Normalize(setting);
        _currentLanguage = Resolve(_currentSetting);

        var uri = _currentLanguage == "en" ? EnUri : DeUri;
        var dict = LoadDictionary(uri);

        var app = Application.Current;
        if (app is null) return;

        if (_activeDict is not null)
        {
            app.Resources.MergedDictionaries.Remove(_activeDict);
        }
        app.Resources.MergedDictionaries.Add(dict);
        _activeDict = dict;

        if (fireEvent)
        {
            Dispatcher.UIThread.Post(() => LanguageChanged?.Invoke());
        }
    }

    private static ResourceDictionary LoadDictionary(string uri)
    {
        var loaded = AvaloniaXamlLoader.Load(new Uri(uri));
        return loaded is ResourceDictionary rd
            ? rd
            : throw new InvalidOperationException($"Resource at '{uri}' is not a ResourceDictionary.");
    }

    private static string Normalize(string? setting) => (setting ?? "auto").Trim().ToLowerInvariant() switch
    {
        "de" => "de",
        "en" => "en",
        _    => "auto",
    };

    private static string Resolve(string normalized) => normalized switch
    {
        "de" or "en" => normalized,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en",
    };
}

/// <summary>
/// Statischer Helper für ViewModels. Liest direkt aus <c>Application.Current.Resources</c>,
/// folgt also automatisch dem aktiven Dictionary. Bei nicht gefundenem Key wird der Key
/// selbst zurückgegeben — das macht fehlende Keys sofort sichtbar.
/// </summary>
public static class Loc
{
    public static string Get(string key)
    {
        var app = Application.Current;
        if (app is null) return key;
        if (app.Resources.TryGetResource(key, app.ActualThemeVariant, out var val) && val is string s)
            return s;
        return key;
    }

    /// <summary>Lookup + string.Format für Werte mit Platzhaltern ({0}, {1}, …).</summary>
    public static string Format(string key, params object?[] args)
    {
        var template = Get(key);
        try { return string.Format(CultureInfo.CurrentCulture, template, args); }
        catch (FormatException) { return template; }
    }
}
