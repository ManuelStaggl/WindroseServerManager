using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.App.ViewModels;

public sealed class ServerStatusToLocalizedTextConverter : IValueConverter
{
    public static readonly ServerStatusToLocalizedTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ServerStatus s ? Loc.Get($"ServerStatus.{s}") : string.Empty;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class WorldPresetTypeToLocalizedTextConverter : IValueConverter
{
    public static readonly WorldPresetTypeToLocalizedTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is WorldPresetType p ? Loc.Get($"WorldPreset.{p}") : string.Empty;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class CombatDifficultyToLocalizedTextConverter : IValueConverter
{
    public static readonly CombatDifficultyToLocalizedTextConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CombatDifficultyOption d ? Loc.Get($"Combat.{d}") : string.Empty;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerStatusToBrushConverter : IValueConverter
{
    public static readonly ServerStatusToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is ServerStatus status ? status switch
        {
            ServerStatus.Running => "BrandSuccessBrush",
            ServerStatus.Starting or ServerStatus.Stopping => "BrandWarningBrush",
            ServerStatus.Crashed => "BrandErrorBrush",
            _ => "BrandTextMutedBrush",
        } : "BrandTextMutedBrush";

        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToInstalledConverter : IValueConverter
{
    public static readonly BoolToInstalledConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Loc.Get("Status.Installed") : Loc.Get("Status.NotInstalled");
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public static readonly ResourceKeyToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
            return Brushes.Gray;

        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class CountToInverseBoolConverter : IValueConverter
{
    public static readonly CountToInverseBoolConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n == 0;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SizeToMbConverter : IValueConverter
{
    public static readonly SizeToMbConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long b ? (b / 1024.0 / 1024.0).ToString("0.0", culture) : "—";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerEventTypeToIconConverter : IValueConverter
{
    public static readonly ServerEventTypeToIconConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ServerEventType t ? t switch
        {
            ServerEventType.Started => "▶",
            ServerEventType.Stopped => "■",
            ServerEventType.Crashed => "⚠",
            ServerEventType.ScheduledRestart => "⟳",
            ServerEventType.AutoRestartHighRam => "⟳",
            ServerEventType.AutoRestartMaxUptime => "⟳",
            _ => "·",
        } : "·";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerEventTypeToBrushConverter : IValueConverter
{
    public static readonly ServerEventTypeToBrushConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is ServerEventType t ? t switch
        {
            ServerEventType.Started => "BrandSuccessBrush",
            ServerEventType.Stopped => "BrandTextMutedBrush",
            ServerEventType.Crashed => "BrandErrorBrush",
            ServerEventType.ScheduledRestart => "BrandInfoBrush",
            ServerEventType.AutoRestartHighRam or ServerEventType.AutoRestartMaxUptime => "BrandWarningBrush",
            _ => "BrandTextMutedBrush",
        } : "BrandTextMutedBrush";

        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerEventToTitleConverter : IValueConverter
{
    public static readonly ServerEventToTitleConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ServerEvent e) return string.Empty;
        var typeLabel = e.Type switch
        {
            ServerEventType.Started => Loc.Get("Event.Started"),
            ServerEventType.Stopped => Loc.Get("Event.Stopped"),
            ServerEventType.Crashed => Loc.Get("Event.Crashed"),
            ServerEventType.ScheduledRestart => Loc.Get("Event.ScheduledRestart"),
            ServerEventType.AutoRestartHighRam => Loc.Get("Event.AutoRestartRam"),
            ServerEventType.AutoRestartMaxUptime => Loc.Get("Event.AutoRestartUptime"),
            _ => e.Type.ToString(),
        };
        var ts = e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", culture);
        return $"{ts}  ·  {typeLabel}";
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerEventToDetailConverter : IValueConverter
{
    public static readonly ServerEventToDetailConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ServerEvent e) return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(e.Reason)) parts.Add(e.Reason);
        if (e.SessionDuration is { } d && d.TotalSeconds > 0)
        {
            var durStr = d.TotalHours >= 1
                ? Loc.Format("Event.SessionDurationHm", (int)d.TotalHours, d.Minutes)
                : Loc.Format("Event.SessionDurationMs", d.Minutes, d.Seconds);
            parts.Add(durStr);
        }
        return string.Join(" · ", parts);
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
