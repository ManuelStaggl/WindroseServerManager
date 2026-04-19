using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.ViewModels;

/// <summary>
/// Gruppiert zusammengehörige .pak-Files.
/// Entweder Nexus-Mod-ID (z.B. "Expanded Horizons" Bundle mit 5 .paks)
/// oder eine einzelne nicht-verlinkte .pak.
/// </summary>
public partial class ModGroupViewModel : ObservableObject
{
    public string GroupKey { get; }                     // "nexus:{id}" oder "single:{filename}"
    public ObservableCollection<ModItemViewModel> Items { get; }
    public string Header { get; }
    public int? NexusModId { get; }

    [ObservableProperty] private bool _isExpanded = true;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ShowExpandedChildren));

    public bool IsBundle => Items.Count > 1;
    public int PakCount => Items.Count;
    public bool HasNexusLink => NexusModId is not null;
    public bool ShowExpandedChildren => IsBundle && IsExpanded;
    public bool ShowChevron => IsBundle;

    /// <summary>Bidirektional bindbar. Getter = AllSelected, Setter toggled alle Items gemeinsam.</summary>
    public bool IsGroupSelected
    {
        get => Items.Count > 0 && Items.All(i => i.IsSelected);
        set
        {
            foreach (var i in Items) i.IsSelected = value;
            OnPropertyChanged();
        }
    }

    public long TotalBytes => Items.Sum(i => i.SizeBytes);
    public int ActiveCount => Items.Count(i => i.IsEnabled);
    public int DisabledCount => Items.Count(i => !i.IsEnabled);
    public bool AllEnabled => Items.Count > 0 && Items.All(i => i.IsEnabled);
    public bool AllDisabled => Items.Count > 0 && Items.All(i => !i.IsEnabled);
    public bool AnySelected => Items.Any(i => i.IsSelected);
    public bool AllSelected => Items.Count > 0 && Items.All(i => i.IsSelected);

    public ModGroupViewModel(string header, string groupKey, int? nexusModId, IEnumerable<ModItemViewModel> items)
    {
        Header = header;
        GroupKey = groupKey;
        NexusModId = nexusModId;
        Items = new ObservableCollection<ModItemViewModel>(items);

        foreach (var i in Items) i.PropertyChanged += OnChildChanged;
        Items.CollectionChanged += (_, _) => RaiseAggregates();
    }

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Aggregate neu auswerten bei relevanten Änderungen
        if (e.PropertyName is nameof(ModItemViewModel.IsSelected)
            or nameof(ModItemViewModel.IsEnabled))
            RaiseAggregates();
    }

    public void RaiseAggregates()
    {
        OnPropertyChanged(nameof(TotalBytes));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(AllEnabled));
        OnPropertyChanged(nameof(AllDisabled));
        OnPropertyChanged(nameof(AnySelected));
        OnPropertyChanged(nameof(AllSelected));
        OnPropertyChanged(nameof(IsGroupSelected));
        OnPropertyChanged(nameof(PakCount));
        OnPropertyChanged(nameof(IsBundle));
    }
}
