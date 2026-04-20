using CommunityToolkit.Mvvm.ComponentModel;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class PlayersViewModel : ViewModelBase
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public PlayersViewModel(IWindrosePlusApiService api, IAppSettingsService settings, IToastService toasts)
    {
        _api = api;
        _settings = settings;
        _toasts = toasts;
    }
}
