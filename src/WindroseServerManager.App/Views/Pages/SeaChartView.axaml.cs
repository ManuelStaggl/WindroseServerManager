using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Pages;

public partial class SeaChartView : UserControl
{
    public SeaChartView() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is SeaChartViewModel vm)
        {
            vm.Start();
            var canvas = this.FindControl<Canvas>("MapCanvas");
            if (canvas is not null)
            {
                canvas.SizeChanged += (_, args) => vm.UpdateCanvasSize(args.NewSize.Width, args.NewSize.Height);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        (DataContext as SeaChartViewModel)?.Stop();
    }

    private void OnMarkerTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control c && c.DataContext is PlayerMarkerViewModel marker)
            marker.SelectCommand.Execute(null);
    }
}
