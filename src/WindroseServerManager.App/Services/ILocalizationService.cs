namespace WindroseServerManager.App.Services;

/// <summary>
/// Läuft über zwei Resource-Dictionaries (DE/EN), tauscht sie zur Laufzeit in
/// Application.Current.Resources.MergedDictionaries. Alle XAML-Bindings via
/// <c>{DynamicResource Key}</c> reagieren live auf den Tausch.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Aktuell aktive Sprache, aufgelöst — "de" oder "en", nie "auto".</summary>
    string CurrentLanguage { get; }

    /// <summary>Rohwert der Einstellung: "auto" | "de" | "en".</summary>
    string CurrentSetting { get; }

    /// <summary>Feuert nach einem Sprachwechsel (auf UI-Thread).</summary>
    event Action? LanguageChanged;

    /// <summary>Lookup im aktuellen Dictionary. Fallback = Key selbst.</summary>
    string this[string key] { get; }

    /// <summary>
    /// Initialisierung beim App-Start mit dem Wert aus AppSettings. Darf nur einmal
    /// gerufen werden — tauscht das initiale Dictionary ein.
    /// </summary>
    void Initialize(string setting);

    /// <summary>
    /// Sprache zur Laufzeit wechseln. Persistenz muss separat über IAppSettingsService
    /// erfolgen — der Service verwaltet nur den UI-State.
    /// </summary>
    void SetLanguage(string setting);
}
