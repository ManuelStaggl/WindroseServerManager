using CommunityToolkit.Mvvm.ComponentModel;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class SeaChartViewModel : ViewModelBase
{
    private readonly IWindrosePlusApiService _api;
    private readonly IAppSettingsService _settings;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public SeaChartViewModel(IWindrosePlusApiService api, IAppSettingsService settings)
    {
        _api = api;
        _settings = settings;
    }
}
