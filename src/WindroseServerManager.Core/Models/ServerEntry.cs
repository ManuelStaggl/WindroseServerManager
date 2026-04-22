namespace WindroseServerManager.Core.Models;

public sealed class ServerEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstallDir { get; set; } = string.Empty;

    /// <summary>
    /// When true, this specific server is auto-started when the app launches.
    /// Effective per-server start condition: <c>AppSettings.AutoStartServerOnAppLaunch || entry.AutoStartOnAppLaunch</c>.
    /// The app-level flag is a shortcut meaning "start all configured servers"; this per-server
    /// flag lets admins pick individual servers instead.
    /// </summary>
    public bool AutoStartOnAppLaunch { get; set; } = false;
}
