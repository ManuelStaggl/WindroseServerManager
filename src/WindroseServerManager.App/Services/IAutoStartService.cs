namespace WindroseServerManager.App.Services;

public interface IAutoStartService
{
    bool IsEnabled();
    void Enable(string exePath);
    void Disable();
}
