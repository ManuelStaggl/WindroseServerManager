namespace WindroseServerManager.Core.Services;

/// <summary>
/// Provides a simple notification interface for system events and user feedback.
/// </summary>
public interface INotificationService
{
    /// <summary>Shows an informational notification.</summary>
    void NotifyInfo(string message);

    /// <summary>Shows a success notification.</summary>
    void NotifySuccess(string message);

    /// <summary>Shows an error notification.</summary>
    void NotifyError(string message);

    /// <summary>Shows a warning notification.</summary>
    void NotifyWarning(string message);
}
