using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WindroseServerManager.App.Services;

public sealed class FirewallService : IFirewallService
{
    private const string RuleName = "Windrose Server";

    private readonly ILogger<FirewallService> _logger;

    public FirewallService(ILogger<FirewallService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsRuleInstalledAsync(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;

        var (exit, output) = await RunNetshAsync(
            $"advfirewall firewall show rule name=\"{RuleName}\" verbose");
        if (exit != 0) return false;

        return output.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public async Task<bool> InstallRuleAsync(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            _logger.LogWarning("Cannot install firewall rule: exe missing at {Path}", exePath);
            return false;
        }

        var inArgs =
            $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow program=\"{exePath}\" enable=yes";
        var outArgs =
            $"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=allow program=\"{exePath}\" enable=yes";

        var (exitIn, _) = await RunNetshAsync(inArgs);
        if (exitIn != 0)
        {
            _logger.LogWarning("netsh add inbound rule failed with exit {Exit}", exitIn);
            return false;
        }

        var (exitOut, _) = await RunNetshAsync(outArgs);
        if (exitOut != 0)
        {
            _logger.LogWarning("netsh add outbound rule failed with exit {Exit}", exitOut);
            return false;
        }

        _logger.LogInformation("Firewall rule installed for {Path}", exePath);
        return true;
    }

    public async Task<bool> RemoveRuleAsync(string exePath)
    {
        var (exit, _) = await RunNetshAsync(
            $"advfirewall firewall delete rule name=\"{RuleName}\"");
        if (exit != 0)
        {
            _logger.LogWarning("netsh delete rule failed with exit {Exit}", exit);
            return false;
        }
        _logger.LogInformation("Firewall rule removed");
        return true;
    }

    private static async Task<(int ExitCode, string Output)> RunNetshAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return (-1, string.Empty);
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (p.ExitCode, stdout + stderr);
        }
        catch (Exception)
        {
            return (-1, string.Empty);
        }
    }

    public static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
