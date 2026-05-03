using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace WindroseServerManager.App.Services;

#pragma warning disable CA1416

public sealed class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindroseServerManager";

    private readonly ILogger<AutoStartService> _logger;

    public AutoStartService(ILogger<AutoStartService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var v = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(v);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read autostart registry value");
            return false;
        }
    }

    public void Enable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key?.SetValue(ValueName, $"\"{exePath}\" --tray", RegistryValueKind.String);
            _logger.LogInformation("Autostart enabled for {Path}", exePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enable autostart");
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.LogInformation("Autostart disabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disable autostart");
        }
    }
}
