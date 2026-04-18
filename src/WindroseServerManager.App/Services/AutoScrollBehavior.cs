using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WindroseServerManager.App.Services;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(AutoScrollBehavior));

    public static bool GetIsEnabled(ScrollViewer sv) => sv.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(ScrollViewer sv, bool value) => sv.SetValue(IsEnabledProperty, value);

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(ScrollViewer sv, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            sv.AttachedToVisualTree += OnAttached;
            HookUp(sv);
        }
        else
        {
            sv.AttachedToVisualTree -= OnAttached;
            Unhook(sv);
        }
    }

    private static void OnAttached(object? sender, EventArgs e)
    {
        if (sender is ScrollViewer sv) HookUp(sv);
    }

    private static readonly Dictionary<ScrollViewer, INotifyCollectionChanged> _subscriptions = new();

    private static void HookUp(ScrollViewer sv)
    {
        var items = FindItemsSource(sv);
        if (items is INotifyCollectionChanged ncc && !_subscriptions.ContainsKey(sv))
        {
            NotifyCollectionChangedEventHandler handler = (_, _) =>
                Dispatcher.UIThread.Post(sv.ScrollToEnd, DispatcherPriority.Background);
            ncc.CollectionChanged += handler;
            _subscriptions[sv] = ncc;
        }
    }

    private static void Unhook(ScrollViewer sv)
    {
        if (_subscriptions.TryGetValue(sv, out var ncc))
        {
            _subscriptions.Remove(sv);
        }
    }

    private static IEnumerable? FindItemsSource(ScrollViewer sv)
    {
        if (sv.Content is ItemsControl ic) return ic.ItemsSource;
        if (sv.FindDescendantOfType<ItemsControl>() is { } found) return found.ItemsSource;
        return null;
    }

    private static T? FindDescendantOfType<T>(this Visual v) where T : Visual
    {
        foreach (var child in v.GetVisualChildren())
        {
            if (child is T t) return t;
            if (child is Visual vc && FindDescendantOfType<T>(vc) is { } nested) return nested;
        }
        return null;
    }
}
