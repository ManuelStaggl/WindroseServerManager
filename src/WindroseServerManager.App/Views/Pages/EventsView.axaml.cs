using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Pages;

public partial class EventsView : UserControl
{
    public EventsView() { AvaloniaXamlLoader.Load(this); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as EventsViewModel)?.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        (DataContext as EventsViewModel)?.Stop();
    }
}
