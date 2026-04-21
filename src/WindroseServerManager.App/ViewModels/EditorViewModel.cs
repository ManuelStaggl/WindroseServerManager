using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _proc;
    private readonly IToastService _toasts;

    [ObservableProperty] private ObservableCollection<CategoryGroup> _categories = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public bool HasAnyError => Categories.Any(c => c.Entries.Any(e => e.HasError));
    public bool CanSave => !HasAnyError && !IsLoading;

    public EditorViewModel(IWindrosePlusApiService api, IAppSettingsService settings, IServerProcessService proc, IToastService toasts)
    {
        _api = api;
        _settings = settings;
        _proc = proc;
        _toasts = toasts;
    }

    public void Start() => _ = LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        var serverDir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var cfg = await Task.Run(() => _api.ReadConfig(serverDir));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Categories.Clear();
                var groups = new Dictionary<string, List<ConfigEntryViewModel>>();
                foreach (var schema in WindrosePlusConfigSchema.All)
                {
                    object? initial = null;
                    if (cfg is not null)
                    {
                        var dict = schema.Category == "Server" ? cfg.Server : cfg.Multipliers;
                        dict.TryGetValue(schema.Key, out initial);
                    }
                    var entry = new ConfigEntryViewModel(schema, initial);
                    entry.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ConfigEntryViewModel.HasError))
                        {
                            OnPropertyChanged(nameof(HasAnyError));
                            OnPropertyChanged(nameof(CanSave));
                            SaveCommand.NotifyCanExecuteChanged();
                        }
                    };
                    if (!groups.TryGetValue(schema.Category, out var list))
                        groups[schema.Category] = list = new List<ConfigEntryViewModel>();
                    list.Add(entry);
                }
                foreach (var (cat, entries) in groups)
                    Categories.Add(new CategoryGroup(cat, entries));
                OnPropertyChanged(nameof(HasAnyError));
                OnPropertyChanged(nameof(CanSave));
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Editor load failed");
            ErrorMessage = Loc.Get("Editor.Error.Load");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanSave));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExecuteSave() => CanSave;

    [RelayCommand(CanExecute = nameof(CanExecuteSave))]
    private async Task SaveAsync()
    {
        var serverDir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;
        if (HasAnyError)
        {
            _toasts.Error(Loc.Get("Editor.ValidationError"));
            return;
        }

        var cfg = _api.ReadConfig(serverDir) ?? new WindrosePlusConfig();
        foreach (var group in Categories)
        {
            var dict = group.Category == "Server" ? cfg.Server : cfg.Multipliers;
            foreach (var entry in group.Entries)
                dict[entry.Key] = entry.ToTypedValue();
        }

        try
        {
            await _api.WriteConfigAsync(serverDir, cfg, CancellationToken.None);
            _toasts.Success(Loc.Get("Editor.Saved"));

            if (_proc.Status == ServerStatus.Running)
                _toasts.Warning(Loc.Get("Editor.RestartRequired"));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Editor save failed");
            _toasts.Error(Loc.Get("Editor.Error.Save"));
        }
    }
}

public sealed class CategoryGroup
{
    public string Category { get; }
    public IReadOnlyList<ConfigEntryViewModel> Entries { get; }

    public CategoryGroup(string category, IReadOnlyList<ConfigEntryViewModel> entries)
    {
        Category = category;
        Entries = entries;
    }
}
