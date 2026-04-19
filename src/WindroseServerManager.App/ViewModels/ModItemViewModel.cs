using CommunityToolkit.Mvvm.ComponentModel;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.App.ViewModels;

public partial class ModItemViewModel : ObservableObject
{
    public ModInfo Info { get; private set; }

    [ObservableProperty] private bool _isSelected;

    /// <summary>Gesetzt nach einem Update-Check. Null = noch nicht gecheckt.</summary>
    [ObservableProperty] private string? _latestNexusVersion;

    public string FileName => Info.FileName;
    public string DisplayName => Info.DisplayName;
    public long SizeBytes => Info.SizeBytes;
    public bool IsEnabled => Info.IsEnabled;
    public IReadOnlyList<string> CompanionFiles => Info.CompanionFiles;
    public ModMeta? NexusMeta => Info.NexusMeta;
    public bool HasNexusLink => Info.NexusMeta is not null;

    /// <summary>True wenn Update-Check gelaufen ist UND neue Version != installierte Version.</summary>
    public bool HasUpdateAvailable =>
        !string.IsNullOrWhiteSpace(LatestNexusVersion) &&
        Info.NexusMeta is { } meta &&
        !string.Equals(LatestNexusVersion, meta.LinkedVersion, StringComparison.OrdinalIgnoreCase);

    public ModItemViewModel(ModInfo info)
    {
        Info = info;
    }

    /// <summary>Ersetzt die zugrundeliegende ModInfo (z.B. nach Verlinkung neu geladen).</summary>
    public void UpdateInfo(ModInfo info)
    {
        Info = info;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(NexusMeta));
        OnPropertyChanged(nameof(HasNexusLink));
        OnPropertyChanged(nameof(HasUpdateAvailable));
    }

    partial void OnLatestNexusVersionChanged(string? value) =>
        OnPropertyChanged(nameof(HasUpdateAvailable));
}
