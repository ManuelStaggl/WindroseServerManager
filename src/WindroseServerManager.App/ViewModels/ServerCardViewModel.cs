using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.ViewModels;

public sealed partial class ServerCardViewModel : ObservableObject
{
    [ObservableProperty] private bool _isActive;

    public string Id { get; }
    public string Name { get; }
    public string InstallDir { get; }

    public ServerCardViewModel(string id, string name, string installDir, bool isActive)
    {
        Id = id;
        Name = name;
        InstallDir = installDir;
        _isActive = isActive;
    }
}
