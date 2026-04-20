using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.App.ViewModels;

public partial class PlayerMarkerViewModel : ObservableObject
{
    [ObservableProperty] private double _canvasX;
    [ObservableProperty] private double _canvasY;
    [ObservableProperty] private WindrosePlusPlayer _player;

    private readonly System.Action<PlayerMarkerViewModel> _onSelect;

    public PlayerMarkerViewModel(WindrosePlusPlayer player, System.Action<PlayerMarkerViewModel> onSelect)
    {
        _player = player;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private void Select() => _onSelect(this);
}
