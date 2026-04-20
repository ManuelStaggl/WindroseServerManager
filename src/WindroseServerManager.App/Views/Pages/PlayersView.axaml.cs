using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Pages;

public partial class PlayersView : UserControl
{
    public PlayersView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as PlayersViewModel)?.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        (DataContext as PlayersViewModel)?.Stop();
    }
}
