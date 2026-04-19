namespace WindroseServerManager.Core.Models;

public sealed class AppSettings
{
    /// <summary>UI-Sprache: "auto" (Windows-Sprache) | "de" | "en".</summary>
    public string Language { get; set; } = "auto";

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

    /// <summary>Max. Zeilen im Live-Log-Puffer (UI). 500 / 2000 / 10000 sind typische Werte.</summary>
    public int LogBufferSize { get; set; } = 2000;

    // Scheduled restart
    public bool ScheduledRestartEnabled { get; set; } = false;
    /// <summary>Format "HH:mm" in local time, 24h.</summary>
    public string DailyRestartTime { get; set; } = "04:00";
    /// <summary>Wochentage an denen der geplante Restart ausgeführt wird. Leer = täglich.</summary>
    public List<DayOfWeek> RestartDays { get; set; } = new();
    /// <summary>Vorwarnzeit in Minuten vor einem geplanten Restart (Toast). 0 = keine Vorwarnung.</summary>
    public int RestartWarnMinutes { get; set; } = 5;

    // Auto-Restart-Schwellen
    public bool AutoRestartOnHighRamEnabled { get; set; } = false;
    /// <summary>RAM-Auslastung des Game-Prozesses in % ab der ein Restart ausgelöst wird.</summary>
    public int AutoRestartRamThresholdPercent { get; set; } = 80;
    public bool AutoRestartOnMaxUptimeEnabled { get; set; } = false;
    /// <summary>Max. Uptime in Stunden, danach Restart.</summary>
    public int AutoRestartMaxUptimeHours { get; set; } = 24;

    // Backups
    public string BackupDir { get; set; } = string.Empty;
    public int AutoBackupIntervalMinutes { get; set; } = 60;
    public bool AutoBackupEnabled { get; set; } = false;
    public int MaxBackupsToKeep { get; set; } = 20;

    // App-Update (GitHub Releases)
    /// <summary>Tag-Name (z.B. "v1.0.1"), den der User via "Später" verworfen hat. Bei neueren Versionen wieder anzeigen.</summary>
    public string DismissedUpdateVersion { get; set; } = "";

    // Nexus Mods — nur noch zum Konstruieren von "Auf Nexus öffnen"-URLs. Kein API-Key, kein API-Call.
    /// <summary>Nexus-Domain-Name des Spiels (für URL-Konstruktion). Windrose = "windrose".</summary>
    public string NexusGameDomain { get; set; } = "windrose";
}
