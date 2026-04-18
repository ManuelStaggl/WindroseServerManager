using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindroseServerManager.App.Services;

public enum ToastKind
{
    Success,
    Warning,
    Error,
    Info,
}

public interface IToastService
{
    ObservableCollection<ToastItem> Toasts { get; }
    void Show(string message, ToastKind kind = ToastKind.Info, int durationMs = 3000);
    void Success(string message);
    void Warning(string message);
    void Error(string message);
    void Info(string message);
}

public sealed partial class ToastItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Message { get; }
    public ToastKind Kind { get; }

    public string BrushKey => Kind switch
    {
        ToastKind.Success => "BrandSuccessBrush",
        ToastKind.Warning => "BrandWarningBrush",
        ToastKind.Error => "BrandErrorBrush",
        _ => "BrandAmberBrush",
    };

    public string Icon => Kind switch
    {
        ToastKind.Success => "\u2713",
        ToastKind.Warning => "!",
        ToastKind.Error => "\u2715",
        _ => "i",
    };

    public ToastItem(string message, ToastKind kind)
    {
        Message = message;
        Kind = kind;
    }
}
