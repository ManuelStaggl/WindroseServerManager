using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Pages;

public partial class ModsView : UserControl
{
    public ModsView()
    {
        AvaloniaXamlLoader.Load(this);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ModsViewModel vm) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;

        foreach (var f in files)
        {
            var path = f.Path.LocalPath;
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".pak" or ".zip" or ".7z")
            {
                await vm.InstallFromPathAsync(path);
            }
        }
    }
}
