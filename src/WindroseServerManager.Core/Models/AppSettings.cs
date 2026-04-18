namespace WindroseServerManager.Core.Models;

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string Language { get; set; } = "de";

    // Server paths
    public string ServerInstallDir { get; set; } = string.Empty;
    public string SteamCmdDir { get; set; } = string.Empty;

    // SteamCMD
    /// <summary>Steam App-ID des Windrose Dedicated Server.</summary>
    public string SteamAppId { get; set; } = "4129620";
    /// <summary>Leer = anonymous login.</summary>
    public string SteamLogin { get; set; } = "";

    // Server runtime
    public bool AutoRestartOnCrash { get; set; } = false;
    public int GracefulShutdownSeconds { get; set; } = 5;

    // Launch-Args (strukturiert)
    public bool LogEnabled { get; set; } = true;
    public string ExtraLaunchArgs { get; set; } = "";

    // Scheduled restart
    public bool ScheduledRestartEnabled { get; set; } = false;
    /// <summary>Format "HH:mm" in local time, 24h.</summary>
    public string DailyRestartTime { get; set; } = "04:00";

    // Backups
    public string BackupDir { get; set; } = string.Empty;
    public int AutoBackupIntervalMinutes { get; set; } = 60;
    public bool AutoBackupEnabled { get; set; } = false;
    public int MaxBackupsToKeep { get; set; } = 20;

    // App-Update (GitHub Releases)
    /// <summary>Tag-Name (z.B. "v1.0.1"), den der User via "Später" verworfen hat. Bei neueren Versionen wieder anzeigen.</summary>
    public string DismissedUpdateVersion { get; set; } = "";
}
