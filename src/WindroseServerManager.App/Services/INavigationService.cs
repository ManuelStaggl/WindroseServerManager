using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Services;

public interface INavigationService
{
    ViewModelBase? Current { get; }
    event Action<ViewModelBase>? Navigated;
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo(ViewModelBase vm);
}
