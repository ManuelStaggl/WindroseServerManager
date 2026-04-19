using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.Services;

public sealed partial class ToastService : ObservableObject, IToastService
{
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public void Show(string message, ToastKind kind = ToastKind.Info, int durationMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var item = new ToastItem(message, kind, Dismiss);

        void AddOnUi()
        {
            Toasts.Add(item);
            DispatcherTimer.RunOnce(() =>
            {
                Toasts.Remove(item);
            }, TimeSpan.FromMilliseconds(Math.Max(500, durationMs)));
        }

        if (Dispatcher.UIThread.CheckAccess())
            AddOnUi();
        else
            Dispatcher.UIThread.Post(AddOnUi);
    }

    private void Dismiss(ToastItem item)
    {
        if (Dispatcher.UIThread.CheckAccess()) Toasts.Remove(item);
        else Dispatcher.UIThread.Post(() => Toasts.Remove(item));
    }

    public void Success(string message) => Show(message, ToastKind.Success, 3000);
    public void Warning(string message) => Show(message, ToastKind.Warning, 6000);
    public void Error(string message) => Show(message, ToastKind.Error, 15000);
    public void Info(string message) => Show(message, ToastKind.Info, 3000);
}
