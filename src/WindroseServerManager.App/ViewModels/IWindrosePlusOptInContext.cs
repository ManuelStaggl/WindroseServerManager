namespace WindroseServerManager.App.ViewModels;

/// <summary>
/// Shared contract consumed by <c>WindrosePlusOptInControl</c>. Implemented by
/// <c>InstallWizardViewModel</c> (Plan 09-02) and the retrofit ViewModels (Plan 09-03)
/// so the UserControl can drive both surfaces identically.
/// </summary>
public interface IWindrosePlusOptInContext
{
    bool IsOptingIn { get; set; }
    string RconPassword { get; set; }
    int DashboardPort { get; }
    string AdminSteamId { get; set; }
    bool HasSteamIdError { get; }
    bool IsSteamIdMissing { get; }
    void RegeneratePassword();
    void ValidateSteamId();
}
