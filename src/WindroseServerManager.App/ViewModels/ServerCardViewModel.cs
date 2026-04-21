using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.ViewModels;

public sealed partial class ServerCardViewModel : ObservableObject
{
    [ObservableProperty] private bool _isActive;

    public string Id { get; }
    public string Name { get; }
    public string InstallDir { get; }
    public bool WindrosePlusActive { get; }

    public ServerCardViewModel(string id, string name, string installDir, bool isActive, bool windrosePlusActive)
    {
        Id = id;
        Name = name;
        InstallDir = installDir;
        _isActive = isActive;
        WindrosePlusActive = windrosePlusActive;
    }
}
