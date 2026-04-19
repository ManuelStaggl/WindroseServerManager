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

public sealed class BoolToCheckGlyphConverter : IValueConverter
{
    public static readonly BoolToCheckGlyphConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "\u2713" : "\u2014";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToAutomaticBrushConverter : IValueConverter
{
    public static readonly BoolToAutomaticBrushConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is bool b && b ? "BrandSuccessBrush" : "BrandTextMutedBrush";
        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush) return brush;
        return Brushes.Gray;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToEnabledBrushConverter : IValueConverter
{
    public static readonly BoolToEnabledBrushConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is bool b && b ? "BrandSuccessBrush" : "BrandTextMutedBrush";
        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush) return brush;
        return Brushes.Gray;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class EnabledToLabelConverter : IValueConverter
{
    public static readonly EnabledToLabelConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Loc.Get("Mods.Card.Enabled") : Loc.Get("Mods.Card.Disabled");
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class EnabledToToggleLabelConverter : IValueConverter
{
    public static readonly EnabledToToggleLabelConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Loc.Get("Mods.Action.Disable") : Loc.Get("Mods.Action.Enable");
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? 1.0 : 0.45;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class EnabledToEyeIconConverter : IValueConverter
{
    public static readonly EnabledToEyeIconConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "\uE7B3" : "\uED1A";  // RedEye / Hide
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToChevronConverter : IValueConverter
{
    public static readonly BoolToChevronConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "\uE70D" : "\uE76C";  // ChevronDown / ChevronRight
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BundleToIconConverter : IValueConverter
{
    public static readonly BundleToIconConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "\uE8B7" : "\uE7B8";  // FolderOpen / Package
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n > 0;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IntEqualsConverter : IValueConverter
{
    public static readonly IntEqualsConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int actual) return false;
        int expected;
        switch (parameter)
        {
            case int i: expected = i; break;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed): expected = parsed; break;
            default: return false;
        }
        return actual == expected;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IntGreaterThanConverter : IValueConverter
{
    public static readonly IntGreaterThanConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int actual) return false;
        int threshold;
        switch (parameter)
        {
            case int i: threshold = i; break;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed): threshold = parsed; break;
            default: return false;
        }
        return actual > threshold;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IntLessThanConverter : IValueConverter
{
    public static readonly IntLessThanConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int actual) return false;
        int threshold;
        switch (parameter)
        {
            case int i: threshold = i; break;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed): threshold = parsed; break;
            default: return false;
        }
        return actual < threshold;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToYesNoConverter : IValueConverter
{
    public static readonly BoolToYesNoConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Loc.Get("Common.Yes") : Loc.Get("Common.No");
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class LogLineToBrushConverter : IValueConverter
{
    public static readonly LogLineToBrushConverter Instance = new();

    private static readonly System.Text.RegularExpressions.Regex ErrorRegex =
        new(@"Log\w+:\s*Error:", System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string line) return ResolveBrush("BrandTextPrimaryBrush");
        string key = ClassifyKey(line);
        return ResolveBrush(key);
    }

    private static string ClassifyKey(string line)
    {
        if (string.IsNullOrEmpty(line)) return "BrandTextPrimaryBrush";
        if (line.Contains("[FEHLER]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("!!!", StringComparison.Ordinal)
            || line.Contains("Error!", StringComparison.Ordinal)
            || ErrorRegex.IsMatch(line))
            return "BrandErrorBrush";
        if (line.Contains("Warning:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[Warn]", StringComparison.OrdinalIgnoreCase))
            return "BrandWarningBrush";
        return "BrandTextPrimaryBrush";
    }

    private static IBrush ResolveBrush(string key)
    {
        var app = Application.Current;
        if (app is not null &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return Brushes.White;
    }

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
