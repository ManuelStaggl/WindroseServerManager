using CommunityToolkit.Mvvm.ComponentModel;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _proc;
    private readonly IToastService _toasts;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public EditorViewModel(IWindrosePlusApiService api, IAppSettingsService settings, IServerProcessService proc, IToastService toasts)
    {
        _api = api;
        _settings = settings;
        _proc = proc;
        _toasts = toasts;
    }
}
