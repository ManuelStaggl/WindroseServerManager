namespace WindroseServerManager.App.Services;

public interface IFirewallService
{
    Task<bool> IsRuleInstalledAsync(string exePath);
    Task<bool> InstallRuleAsync(string exePath);
    Task<bool> RemoveRuleAsync(string exePath);
}
