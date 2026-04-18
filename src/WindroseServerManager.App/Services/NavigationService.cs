using Microsoft.Extensions.DependencyInjection;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _sp;

    public NavigationService(IServiceProvider sp)
    {
        _sp = sp;
    }

    public ViewModelBase? Current { get; private set; }
    public event Action<ViewModelBase>? Navigated;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vm = _sp.GetRequiredService<TViewModel>();
        NavigateTo(vm);
    }

    public void NavigateTo(ViewModelBase vm)
    {
        Current = vm;
        Navigated?.Invoke(vm);
    }
}
