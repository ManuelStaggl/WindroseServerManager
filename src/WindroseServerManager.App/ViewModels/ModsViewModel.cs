using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class ModsViewModel : ViewModelBase
{
    private readonly IModService _mods;
    private readonly IServerProcessService _proc;
    private readonly IToastService _toasts;
    private readonly IAppSettingsService _settings;

    public ObservableCollection<ModItemViewModel> Mods { get; } = new();
    public ObservableCollection<ModGroupViewModel> Groups { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _modsDirectory = string.Empty;
    [ObservableProperty] private string? _readinessMessage;
    [ObservableProperty] private bool _isReady;

    public bool HasNoMods => Mods.Count == 0;
    public bool HasMods => Mods.Count > 0;
    public int SelectedCount => Mods.Count(m => m.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public bool AllSelected => Mods.Count > 0 && Mods.All(m => m.IsSelected);

    public ModsViewModel(
        IModService mods,
        IServerProcessService proc,
        IToastService toasts,
        IAppSettingsService settings)
    {
        _mods = mods;
        _proc = proc;
        _toasts = toasts;
        _settings = settings;

        _proc.StatusChanged += _ => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshReadiness);
        Mods.CollectionChanged += OnModsCollectionChanged;

        Refresh();
        RefreshReadiness();
    }

    private static Window? GetOwnerWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow
            : null;

    private void OnModsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (ModItemViewModel m in e.OldItems) m.PropertyChanged -= OnItemPropertyChanged;
        if (e.NewItems is not null)
            foreach (ModItemViewModel m in e.NewItems) m.PropertyChanged += OnItemPropertyChanged;
        RaiseSelectionState();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModItemViewModel.IsSelected))
            RaiseSelectionState();
    }

    private void RaiseSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(AllSelected));
    }

    private void ReportError(Exception ex) =>
        _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex));

    [RelayCommand]
    private void Refresh()
    {
        // Alte Expand-States merken (pro Gruppen-Key), damit beim Refresh die UI nicht "zuklappt"
        var expansionStates = Groups.ToDictionary(g => g.GroupKey, g => g.IsExpanded);

        Mods.Clear();
        Groups.Clear();
        try
        {
            ModsDirectory = _mods.GetModsDir() ?? string.Empty;

            var items = _mods.ListMods()
                .OrderBy(m => m.DisplayName)
                .Select(m => new ModItemViewModel(m))
                .ToList();

            foreach (var i in items) Mods.Add(i);

            RebuildGroups(items, expansionStates);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            OnPropertyChanged(nameof(HasNoMods));
            OnPropertyChanged(nameof(HasMods));
            RaiseSelectionState();
        }
    }

    private void RebuildGroups(List<ModItemViewModel> items, Dictionary<string, bool> prevExpanded)
    {
        // Bundles (gleiche Nexus-Mod-ID) zu einer Gruppe zusammenfassen;
        // alles ohne Nexus-Link bleibt als eigene Ein-Item-Gruppe.
        var bundles = items
            .Where(i => i.NexusMeta is not null)
            .GroupBy(i => i.NexusMeta!.NexusModId)
            .Select(g =>
            {
                var header = g.First().DisplayName;
                var key = $"nexus:{g.Key}";
                var grp = new ModGroupViewModel(header, key, g.Key, g);
                if (prevExpanded.TryGetValue(key, out var wasExp))
                    grp.IsExpanded = wasExp;
                else
                    grp.IsExpanded = true;
                return grp;
            });

        var singles = items
            .Where(i => i.NexusMeta is null)
            .Select(i =>
            {
                var key = $"single:{i.FileName}";
                var grp = new ModGroupViewModel(i.DisplayName, key, null, new[] { i });
                if (prevExpanded.TryGetValue(key, out var wasExp))
                    grp.IsExpanded = wasExp;
                return grp;
            });

        foreach (var g in bundles.Concat(singles).OrderBy(g => g.Header))
            Groups.Add(g);
    }

    private void RefreshReadiness()
    {
        ReadinessMessage = _mods.ValidateReady();
        IsReady = ReadinessMessage is null;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;

        var picks = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Get("Mods.Install.Title"),
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Windrose Mod")
                {
                    Patterns = new[] { "*.pak", "*.zip", "*.7z" }
                }
            }
        });
        if (picks.Count == 0) return;

        foreach (var f in picks)
        {
            await InstallFromPathAsync(f.Path.LocalPath);
        }
    }

    public async Task InstallFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var ready = _mods.ValidateReady();
        if (ready is not null)
        {
            _toasts.Warning(ready);
            return;
        }

        IsBusy = true;
        try
        {
            var mods = await _mods.InstallFromArchiveAsync(path);
            _toasts.Success(mods.Count == 1
                ? Loc.Format("Toast.ModInstalledFormat", mods[0].DisplayName)
                : Loc.Format("Toast.ModInstalledCountFormat", mods.Count));

            // Auto-Link: wenn der Archivname eine Nexus-Mod-ID enthält, alle
            // neu installierten .paks damit verknüpfen (pure Filename-Analyse, kein Netzwerk).
            TryAutoLinkAll(mods.Select(m => m.FileName).ToList(), Path.GetFileName(path));

            Refresh();
        }
        catch (Exception ex)
        {
            var archiveName = Path.GetFileName(path);
            var baseMsg = ErrorMessageHelper.FriendlyMessage(ex);
            _toasts.Error(Loc.Format("Mods.Error.ArchiveFormat", archiveName, baseMsg));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void TryAutoLinkAll(IReadOnlyList<string> installedPakFileNames, string originalArchiveFileName)
    {
        if (installedPakFileNames.Count == 0) return;

        var modId = NexusUrlParser.TryExtractModIdFromArchiveName(originalArchiveFileName);
        if (modId <= 0) return;

        try
        {
            var meta = new ModMeta(modId, DateTime.UtcNow);
            foreach (var file in installedPakFileNames)
                _mods.SetMeta(file, meta);

            _toasts.Info(Loc.Format("Toast.NexusAutoLinkedFormat", modId));
        }
        catch
        {
            // Auto-Link ist best-effort — Fehler ignorieren, User kann manuell verlinken.
        }
    }

    [RelayCommand]
    private void ToggleEnabled(ModItemViewModel? mod)
    {
        if (mod is null) return;
        var ready = _mods.ValidateReady();
        if (ready is not null) { _toasts.Warning(ready); return; }

        try
        {
            _mods.SetEnabled(mod.FileName, !mod.IsEnabled);
            _toasts.Info(mod.IsEnabled
                ? Loc.Format("Toast.ModDisabledFormat", mod.DisplayName)
                : Loc.Format("Toast.ModEnabledFormat", mod.DisplayName));
            Refresh();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(ModItemViewModel? mod)
    {
        if (mod is null) return;
        var ready = _mods.ValidateReady();
        if (ready is not null) { _toasts.Warning(ready); return; }

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                Loc.Get("Confirm.ModUninstall.Title"),
                Loc.Format("Confirm.ModUninstall.MessageFormat", mod.DisplayName),
                confirmLabel: Loc.Get("Confirm.ModUninstall.Label"),
                danger: true);
            if (!confirmed) return;
        }

        try
        {
            _mods.UninstallMod(mod.FileName);
            _toasts.Success(Loc.Format("Toast.ModUninstalledFormat", mod.DisplayName));
            Refresh();
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        var selected = Mods.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0) return;

        var ready = _mods.ValidateReady();
        if (ready is not null) { _toasts.Warning(ready); return; }

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                Loc.Get("Confirm.ModBulkUninstall.Title"),
                Loc.Format("Confirm.ModBulkUninstall.MessageFormat", selected.Count),
                confirmLabel: Loc.Get("Confirm.ModUninstall.Label"),
                danger: true);
            if (!confirmed) return;
        }

        var failed = 0;
        foreach (var mod in selected)
        {
            try { _mods.UninstallMod(mod.FileName); }
            catch { failed++; }
        }

        if (failed == 0)
            _toasts.Success(Loc.Format("Toast.ModBulkUninstalledFormat", selected.Count));
        else
            _toasts.Warning(Loc.Format("Toast.ModBulkUninstalledPartialFormat", selected.Count - failed, failed));

        Refresh();
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var newState = !AllSelected;
        foreach (var m in Mods) m.IsSelected = newState;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var m in Mods) m.IsSelected = false;
    }

    [RelayCommand]
    private async Task ExportClientBundleAsync()
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;

        var activeCount = Mods.Count(m => m.IsEnabled);
        if (activeCount == 0)
        {
            _toasts.Warning(Loc.Get("Toast.ModExportNothing"));
            return;
        }

        var pick = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.Get("Mods.ExportClient.Title"),
            SuggestedFileName = $"windrose-client-mods-{DateTime.Now:yyyyMMdd}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("ZIP") { Patterns = new[] { "*.zip" } }
            }
        });
        if (pick is null) return;

        IsBusy = true;
        try
        {
            var path = await _mods.ExportClientBundleAsync(pick.Path.LocalPath);
            _toasts.Success(Loc.Format("Toast.ModExportedFormat", activeCount, path));
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LinkNexusAsync(ModItemViewModel? mod)
    {
        if (mod is null) return;

        var owner = GetOwnerWindow();
        if (owner is null) return;

        var modId = await LinkNexusDialog.ShowAsync(
            owner, _settings.Current.NexusGameDomain, mod.DisplayName);
        if (modId is null) return;

        try
        {
            _mods.SetMeta(mod.FileName, new ModMeta(modId.Value, DateTime.UtcNow));
            _toasts.Success(Loc.Format("Toast.NexusLinkedFormat", modId.Value));
            Refresh();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void UnlinkNexus(ModItemViewModel? mod)
    {
        if (mod is null || !mod.HasNexusLink) return;
        try
        {
            _mods.ClearMeta(mod.FileName);
            _toasts.Info(Loc.Format("Toast.NexusUnlinkedFormat", mod.DisplayName));
            Refresh();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void OpenOnNexus(ModItemViewModel? mod)
    {
        if (mod?.NexusMeta is not { } meta) return;
        var url = $"https://www.nexusmods.com/{_settings.Current.NexusGameDomain}/mods/{meta.NexusModId}";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void ToggleGroupExpanded(ModGroupViewModel? group)
    {
        if (group is null) return;
        group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand]
    private void SetGroupEnabled(object? param)
    {
        if (param is not ValueTuple<ModGroupViewModel, bool> t) return;
        var (group, enabled) = t;
        var ready = _mods.ValidateReady();
        if (ready is not null) { _toasts.Warning(ready); return; }

        try
        {
            foreach (var item in group.Items)
            {
                if (item.IsEnabled != enabled)
                    _mods.SetEnabled(item.FileName, enabled);
            }
            _toasts.Info(enabled
                ? Loc.Format("Toast.GroupEnabledFormat", group.Header)
                : Loc.Format("Toast.GroupDisabledFormat", group.Header));
            Refresh();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void EnableGroup(ModGroupViewModel? group)
    {
        if (group is null) return;
        SetGroupEnabled((group, true));
    }

    [RelayCommand]
    private void DisableGroup(ModGroupViewModel? group)
    {
        if (group is null) return;
        SetGroupEnabled((group, false));
    }

    [RelayCommand]
    private async Task UninstallGroupAsync(ModGroupViewModel? group)
    {
        if (group is null) return;
        var ready = _mods.ValidateReady();
        if (ready is not null) { _toasts.Warning(ready); return; }

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                Loc.Get("Confirm.ModBulkUninstall.Title"),
                Loc.Format("Confirm.ModBulkUninstall.MessageFormat", group.Items.Count),
                confirmLabel: Loc.Get("Confirm.ModUninstall.Label"),
                danger: true);
            if (!confirmed) return;
        }

        try
        {
            foreach (var item in group.Items.ToList())
                _mods.UninstallMod(item.FileName);
            _toasts.Success(Loc.Format("Toast.ModBulkUninstalledFormat", group.Items.Count));
            Refresh();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void UnlinkGroup(ModGroupViewModel? group)
    {
        if (group is null) return;
        try
        {
            foreach (var item in group.Items) _mods.ClearMeta(item.FileName);
            _toasts.Info(Loc.Format("Toast.NexusUnlinkedFormat", group.Header));
            Refresh();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void OpenGroupOnNexus(ModGroupViewModel? group)
    {
        if (group?.NexusModId is null) return;
        var url = $"https://www.nexusmods.com/{_settings.Current.NexusGameDomain}/mods/{group.NexusModId}";
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { ReportError(ex); }
    }

    [RelayCommand]
    private void OpenModsDir()
    {
        try
        {
            var dir = _mods.GetModsDir();
            if (string.IsNullOrWhiteSpace(dir)) return;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex) { _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex)); }
    }
}
