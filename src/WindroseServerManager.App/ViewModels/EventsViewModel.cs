using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class EventsViewModel : ViewModelBase, IDisposable
{
    private readonly IWindrosePlusApiService _api;
    private readonly IWindrosePlusService _wplus;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private FileSystemWatcher? _watcher;
    private long _lastReadPosition;
    private string? _logPath;
    private CancellationTokenSource? _debounceCts;
    private readonly SemaphoreSlim _readLock = new(1, 1);

    [ObservableProperty] private ObservableCollection<WindrosePlusEvent> _events = new();
    [ObservableProperty] private ObservableCollection<WindrosePlusEvent> _filteredEvents = new();
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public bool IsWindrosePlusActive =>
        _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(
            _settings.ActiveServerDir ?? string.Empty, false);

    public bool HasEvents => FilteredEvents.Count > 0;
    public bool HasNoEvents => !IsLoading && FilteredEvents.Count == 0;

    public EventsViewModel(IWindrosePlusApiService api, IWindrosePlusService wplus, IAppSettingsService settings, IToastService toasts)
    {
        _api = api;
        _wplus = wplus;
        _settings = settings;
        _toasts = toasts;
        FilteredEvents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasEvents));
            OnPropertyChanged(nameof(HasNoEvents));
        };
    }

    partial void OnFilterTextChanged(string value) => RebuildFilter();

    public void Start()
    {
        OnPropertyChanged(nameof(IsWindrosePlusActive));
        var serverDir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(serverDir)) return;
        if (!IsWindrosePlusActive) return;
        var dir = Path.Combine(serverDir, "windrose_plus_data");
        _logPath = Path.Combine(dir, "events.log");

        Events.Clear();
        FilteredEvents.Clear();

        if (!Directory.Exists(dir))
        {
            _lastReadPosition = 0;
            return;
        }

        if (!File.Exists(_logPath))
        {
            _lastReadPosition = 0;
        }

        // Initial tail-read of existing events
        _ = Task.Run(InitialLoadAsync);

        try
        {
            _watcher = new FileSystemWatcher(dir, "events.log")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnLogChanged;
            _watcher.Created += OnLogChanged;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FileSystemWatcher init failed for {Dir}", dir);
        }
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnLogChanged;
            _watcher.Created -= OnLogChanged;
            _watcher.Dispose();
            _watcher = null;
        }
        _debounceCts?.Cancel();
    }

    private async Task InitialLoadAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsLoading = true);
        if (_logPath is null || !File.Exists(_logPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            return;
        }
        try
        {
            await _readLock.WaitAsync().ConfigureAwait(false);
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            var buffer = new System.Collections.Generic.List<WindrosePlusEvent>();
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                var evt = EventsLogParser.TryParseLine(line);
                if (evt is not null) buffer.Add(evt);
            }
            _lastReadPosition = fs.Position;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var e in buffer) Events.Add(e);
                RebuildFilter();
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Initial events.log read failed");
        }
        finally
        {
            _readLock.Release();
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void OnLogChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: FileSystemWatcher fires 2-3x per write (LastWrite + Size)
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, cts.Token).ConfigureAwait(false);
                await ReadNewLinesAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* newer event arrived, ignore */ }
        });
    }

    private async Task ReadNewLinesAsync()
    {
        if (_logPath is null || !File.Exists(_logPath)) return;
        try
        {
            await _readLock.WaitAsync().ConfigureAwait(false);
            var info = new FileInfo(_logPath);
            if (info.Length < _lastReadPosition)
            {
                // Log rotated/truncated -- reset
                _lastReadPosition = 0;
                await Dispatcher.UIThread.InvokeAsync(() => { Events.Clear(); FilteredEvents.Clear(); });
            }

            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastReadPosition, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            string? line;
            var newOnes = new System.Collections.Generic.List<WindrosePlusEvent>();
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                var evt = EventsLogParser.TryParseLine(line);
                if (evt is not null) newOnes.Add(evt);
            }
            _lastReadPosition = fs.Position;

            if (newOnes.Count == 0) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var e in newOnes) Events.Add(e);
                RebuildFilter();
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Events.log incremental read failed");
        }
        finally { _readLock.Release(); }
    }

    private void RebuildFilter()
    {
        var filter = FilterText ?? string.Empty;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RebuildFilter);
            return;
        }
        FilteredEvents.Clear();
        foreach (var e in Events.Where(e => EventsLogParser.MatchesFilter(e, filter)))
            FilteredEvents.Add(e);
    }

    [RelayCommand]
    private async Task InstallWindrosePlusAsync()
    {
        var dir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(dir)) return;
        var top = (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (top is null) return;
        var bannerVm = new RetrofitBannerViewModel(dir, _wplus, _api, _settings, _toasts);
        var dialog = new RetrofitDialog { DataContext = bannerVm };
        var confirmed = await dialog.ShowDialog<bool>(top);
        if (confirmed)
        {
            OnPropertyChanged(nameof(IsWindrosePlusActive));
            Start();
        }
    }

    public void Dispose()
    {
        Stop();
        _readLock.Dispose();
        _debounceCts?.Dispose();
    }
}
