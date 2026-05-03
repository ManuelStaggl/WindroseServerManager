using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.Services;

/// <summary>
/// Adapter that implements INotificationService using the existing IToastService.
/// Allows Core services to use notifications without direct dependency on Avalonia.
/// </summary>
public sealed class NotificationServiceAdapter : INotificationService
{
    private readonly IToastService _toastService;

    public NotificationServiceAdapter(IToastService toastService)
    {
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
    }

    public void NotifyInfo(string message) => _toastService.Info(message);
    public void NotifySuccess(string message) => _toastService.Success(message);
    public void NotifyError(string message) => _toastService.Error(message);
    public void NotifyWarning(string message) => _toastService.Warning(message);
}
