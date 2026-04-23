using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class InstallationViewModel : ViewModelBase, IWindrosePlusOptInContext
{
    private readonly IServerInstallService _install;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private readonly IWindrosePlusService _wplus;
    private readonly IWindrosePlusApiService _wplusApi;
    private readonly INavigationService _nav;
    private readonly IServerProcessService _process;
    private readonly IWindrosePlusUpdateService _wplusUpdate;
    private readonly IServerConfigService _config;
    private System.Threading.CancellationTokenSource? _cts;

    // ── Server list ───────────────────────────────────────────────────────────
    public ObservableCollection<ServerCardViewModel> ServerCards { get; } = new();

    // Aggregate WindrosePlus-update state for the header banner.
    [ObservableProperty] private bool _windrosePlusUpdateBannerVisible;
    [ObservableProperty] private int _windrosePlusUpdatePendingCount;
    [ObservableProperty] private string? _windrosePlusLatestTag;
    [ObservableProperty] private bool _isUpdatingAllWindrosePlus;
    [ObservableProperty] private string _windrosePlusUpdateBannerTitle = string.Empty;
    [ObservableProperty] private string _windrosePlusUpdateBannerBody = string.Empty;

    // ── Wizard state ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isAddingServer;
    [ObservableProperty] private int _wizardStep = 1;

    // Step 1
    [ObservableProperty] private string _newServerName = string.Empty;
    [ObservableProperty] private string _newInstallDir = string.Empty;
    [ObservableProperty] private string? _step1Error;

    /// <summary>
    /// True, wenn unter <see cref="NewInstallDir"/> bereits eine Windrose-Server-Binary liegt
    /// (z. B. aus einer manuellen SteamCMD-Installation). Der Wizard überspringt dann den
    /// SteamCMD-Download und adoptiert die bestehende Installation.
    /// </summary>
    [ObservableProperty] private bool _isExistingInstall;

    /// <summary>
    /// True, wenn unter <see cref="NewInstallDir"/> bereits ein im Manager registrierter Server liegt.
    /// In dem Fall blockieren wir den Wizard, sonst entstehen doppelte Einträge auf denselben Ordner.
    /// </summary>
    [ObservableProperty] private bool _isDuplicateInstall;
    [ObservableProperty] private string? _duplicateServerName;

    // Step 2 – IWindrosePlusOptInContext
    [ObservableProperty] private bool _isOptingIn = true;
    [ObservableProperty] private string _rconPassword = string.Empty;
    [ObservableProperty] private int _dashboardPort;
    [ObservableProperty] private string _adminSteamId = string.Empty;
    [ObservableProperty] private bool _hasSteamIdError;

    public bool IsSteamIdMissing => IsOptingIn && string.IsNullOrWhiteSpace(AdminSteamId);
    public bool ShowInstallButton => WizardStep == 3 && !InstallCompleted;

    // Step 3
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _currentPhase = string.Empty;
    [ObservableProperty] private bool _installCompleted;
    [ObservableProperty] private bool _hasWizardError;
    [ObservableProperty] private string? _wizardError;

    public InstallationViewModel(
        IServerInstallService install,
        IAppSettingsService settings,
        IToastService toasts,
        IWindrosePlusService wplus,
        IWindrosePlusApiService wplusApi,
        INavigationService nav,
        IServerProcessService process,
        ILocalizationService localization,
        IWindrosePlusUpdateService wplusUpdate,
        IServerConfigService config)
    {
        _install = install;
        _settings = settings;
        _toasts = toasts;
        _wplus = wplus;
        _wplusApi = wplusApi;
        _nav = nav;
        _process = process;
        _wplusUpdate = wplusUpdate;
        _config = config;

        RefreshServerCards();
        _settings.Changed += _ => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshServerCards);
        localization.LanguageChanged += RefreshServerCards;
        _process.StatusChanged += _ => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshServerCards);
        _wplusUpdate.UpdateChecked += _ => Avalonia.Threading.Dispatcher.UIThread.Post(ApplyUpdateStatusToCards);
    }

    private void ApplyUpdateStatusToCards()
    {
        var result = _wplusUpdate.LastResult;
        if (result is null)
        {
            WindrosePlusUpdateBannerVisible = false;
            WindrosePlusUpdatePendingCount = 0;
            WindrosePlusLatestTag = null;
            return;
        }
        foreach (var card in ServerCards)
        {
            var status = result.Servers.FirstOrDefault(x => x.ServerId == card.Id);
            if (status is null)
            {
                card.HasWindrosePlusUpdate = false;
                card.InstalledWindrosePlusTag = null;
                card.LatestWindrosePlusTag = null;
            }
            else
            {
                card.InstalledWindrosePlusTag = status.InstalledTag;
                card.LatestWindrosePlusTag = result.LatestTag;
                card.HasWindrosePlusUpdate = status.HasUpdate;
            }
        }
        WindrosePlusUpdatePendingCount = result.Servers.Count(x => x.HasUpdate);
        WindrosePlusLatestTag = result.LatestTag;
        WindrosePlusUpdateBannerVisible = result.AnyUpdate;
        WindrosePlusUpdateBannerTitle = result.LatestTag is null
            ? string.Empty
            : Loc.Format("Server.WindrosePlus.Update.BannerTitleFormat", result.LatestTag);
        WindrosePlusUpdateBannerBody = Loc.Format("Server.WindrosePlus.Update.BannerBodyFormat", WindrosePlusUpdatePendingCount);
    }

    private void RefreshServerCards()
    {
        // Unsubscribe from the previous cards to avoid duplicate handlers across refreshes.
        foreach (var existing in ServerCards)
            existing.AutoStartChanged -= OnCardAutoStartChanged;

        ServerCards.Clear();
        var activeId = _settings.Current.ActiveServerId;
        foreach (var s in _settings.Current.Servers)
        {
            // Physical marker beats the settings flag — the flag can lie after a broken install.
            var wpInstalled = File.Exists(Path.Combine(s.InstallDir, ".wplus-version"));
            var isActive    = s.Id == activeId;
            var isRunning   = isActive && _process.Status is ServerStatus.Running or ServerStatus.Starting;

            // Live-map URL only meaningful when WindrosePlus is opted-in AND we have a dashboard port.
            string? liveMapUrl = null;
            var optedIn = _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(s.InstallDir, false);
            if (optedIn)
            {
                var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(s.InstallDir, 0);
                if (port > 0) liveMapUrl = $"http://localhost:{port}/livemap";
            }

            var card = new ServerCardViewModel(
                s.Id, s.Name, s.InstallDir,
                isActive, wpInstalled,
                s.AutoStartOnAppLaunch,
                isRunning,
                liveMapUrl);
            card.AutoStartChanged += OnCardAutoStartChanged;
            ServerCards.Add(card);
        }
        ApplyUpdateStatusToCards();
    }

    private void OnCardAutoStartChanged(string serverId, bool value)
    {
        // Persist per-server autostart flag when the toggle is flipped in the UI.
        _ = _settings.UpdateAsync(s =>
        {
            var entry = s.Servers.FirstOrDefault(e => e.Id == serverId);
            if (entry is not null) entry.AutoStartOnAppLaunch = value;
        });
    }

    // ── IWindrosePlusOptInContext ─────────────────────────────────────────────
    partial void OnWizardStepChanged(int value) => OnPropertyChanged(nameof(ShowInstallButton));
    partial void OnInstallCompletedChanged(bool value) => OnPropertyChanged(nameof(ShowInstallButton));

    partial void OnIsOptingInChanged(bool value)
    {
        if (!value) HasSteamIdError = false;
        OnPropertyChanged(nameof(IsSteamIdMissing));
    }

    partial void OnAdminSteamIdChanged(string value) => OnPropertyChanged(nameof(IsSteamIdMissing));

    partial void OnNewInstallDirChanged(string value)
    {
        var trimmed = value?.Trim();
        IsExistingInstall =
            !string.IsNullOrWhiteSpace(trimmed) &&
            Directory.Exists(trimmed) &&
            ServerInstallService.FindServerBinary(trimmed!) is not null;

        // Doppelter Eintrag auf den gleichen Ordner — vergleiche normalisiert,
        // damit Trailing-Slash / Casing nicht durchrutscht.
        var normalized = string.IsNullOrWhiteSpace(trimmed)
            ? string.Empty
            : trimmed!.TrimEnd('\\', '/');
        var dup = string.IsNullOrEmpty(normalized)
            ? null
            : _settings.Current.Servers.FirstOrDefault(s =>
                string.Equals(
                    s.InstallDir?.TrimEnd('\\', '/'),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
        IsDuplicateInstall = dup is not null;
        DuplicateServerName = dup?.Name;
    }

    public void RegeneratePassword() => RconPassword = RconPasswordGenerator.Generate(24);

    public void ValidateSteamId()
    {
        if (!IsOptingIn || string.IsNullOrWhiteSpace(AdminSteamId)) { HasSteamIdError = false; return; }
        HasSteamIdError = SteamIdParser.ExtractSteamId64(AdminSteamId) is null;
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void AddServer()
    {
        NewServerName = string.Empty;
        NewInstallDir = string.Empty;
        Step1Error = null;
        IsExistingInstall = false;
        IsDuplicateInstall = false;
        DuplicateServerName = null;
        IsOptingIn = true;
        RconPassword = RconPasswordGenerator.Generate(24);
        DashboardPort = FreePortProbe.FindFreePort();
        AdminSteamId = string.Empty;
        HasSteamIdError = false;
        IsInstalling = false;
        InstallCompleted = false;
        HasWizardError = false;
        WizardError = null;
        CurrentPhase = string.Empty;
        WizardStep = 1;
        IsAddingServer = true;
    }

    [RelayCommand]
    private void CancelAdd()
    {
        _cts?.Cancel();
        IsAddingServer = false;
    }

    [RelayCommand]
    private async Task BrowseDir()
    {
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top is null) return;
        var picks = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Get("Installation.PickFolder.Title"),
            AllowMultiple = false,
        });
        if (picks.Count > 0)
            NewInstallDir = picks[0].Path.LocalPath;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (WizardStep == 1)
        {
            if (string.IsNullOrWhiteSpace(NewServerName))
            {
                Step1Error = Loc.Get("Server.Name.Required");
                return;
            }
            Step1Error = _install.ValidateInstallDir(NewInstallDir);
            if (Step1Error is not null) return;
            if (IsDuplicateInstall)
            {
                Step1Error = Loc.Format("Wizard.Adoption.DuplicateError", DuplicateServerName ?? "?");
                return;
            }
        }
        if (WizardStep < 3)
            WizardStep++;
    }

    [RelayCommand]
    private void PrevStep()
    {
        if (WizardStep > 1) WizardStep--;
    }

    [RelayCommand]
    private async Task Install()
    {
        HasWizardError = false;
        WizardError = null;

        // Safety: bei Adoption mit WindrosePlus-Reinstall müssen wir DLLs überschreiben können —
        // das geht nicht, wenn ein Server-Prozess die Dateien hält.
        if (IsExistingInstall && IsOptingIn &&
            ServerInstallService.IsServerProcessRunning(NewInstallDir))
        {
            WizardError = Loc.Get("Wizard.Adoption.ServerRunningError");
            HasWizardError = true;
            _toasts.Error(WizardError);
            return;
        }

        IsInstalling = true;
        InstallCompleted = false;
        _cts = new System.Threading.CancellationTokenSource();
        try
        {
            // 1) SteamCMD install — bei adoptierter Installation überspringen, sonst würde
            //    +app_update validate eine bestehende manuelle Installation erneut ziehen.
            if (IsExistingInstall)
            {
                CurrentPhase = Loc.Get("Wizard.Adoption.Progress");
            }
            else
            {
                await foreach (var p in _install.InstallOrUpdateAsync(NewInstallDir, _cts.Token))
                {
                    CurrentPhase = string.IsNullOrWhiteSpace(p.Message)
                        ? Loc.Get($"InstallPhase.{p.Phase}")
                        : $"{Loc.Get($"InstallPhase.{p.Phase}")}: {p.Message}";

                    if (p.Phase == InstallPhase.Failed)
                    {
                        WizardError = p.Message;
                        HasWizardError = true;
                        _toasts.Error(p.Message);
                        return;
                    }
                }
            }

            // 2) WindrosePlus (optional)
            if (IsOptingIn)
            {
                var progress = new Progress<InstallProgress>(p =>
                {
                    CurrentPhase = p.Phase switch
                    {
                        InstallPhase.DownloadingArchive => Loc.Get("Wizard.WindrosePlus.Progress.Downloading"),
                        InstallPhase.Extracting => Loc.Get("Wizard.WindrosePlus.Progress.Extracting"),
                        InstallPhase.Installing => Loc.Get("Wizard.WindrosePlus.Progress.Installing"),
                        _ => string.IsNullOrWhiteSpace(p.Message) ? Loc.Get($"InstallPhase.{p.Phase}") : p.Message,
                    };
                });
                await _wplus.InstallAsync(NewInstallDir, progress, _cts.Token);

                var cfg = _wplusApi.ReadConfig(NewInstallDir) ?? new WindrosePlusConfig();
                cfg.Server["http_port"] = DashboardPort;
                cfg.Rcon["enabled"] = true;
                cfg.Rcon["password"] = RconPassword;
                await _wplusApi.WriteConfigAsync(NewInstallDir, cfg, _cts.Token);
            }

            // 3) ServerDescription.json mit gewähltem Namen + Invite-Code anlegen,
            //    damit Dashboard/Configuration den richtigen Namen anzeigen und
            //    frisch installierte Server von Anfang an einen Invite-Code haben.
            //    Wenn bereits eine Datei existiert (adoptierter Server), füllen wir
            //    nur leere Felder auf — ein bestehender Invite-Code bleibt unangetastet.
            try
            {
                var existing = await _config.LoadServerDescriptionFromAsync(NewInstallDir, _cts.Token)
                               ?? new ServerDescription();
                if (string.IsNullOrWhiteSpace(existing.ServerName))
                    existing.ServerName = NewServerName;
                if (string.IsNullOrWhiteSpace(existing.InviteCode))
                    existing.InviteCode = InviteCodeGenerator.Generate();
                await _config.SaveServerDescriptionToAsync(NewInstallDir, existing, _cts.Token);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Konnte ServerDescription.json nicht vor-initialisieren — Server wird Defaults schreiben");
            }

            // 4) Persist server entry + per-server state
            var installDir = NewInstallDir.TrimEnd('\\', '/');
            var name = NewServerName;
            var optedIn = IsOptingIn;
            var port = DashboardPort;
            var rcon = RconPassword;
            var steamId = SteamIdParser.ExtractSteamId64(AdminSteamId);

            await _settings.UpdateAsync(s =>
            {
                var entry = new ServerEntry
                {
                    Id = System.Guid.NewGuid().ToString("N")[..8],
                    Name = name,
                    InstallDir = installDir,
                };
                s.Servers.Add(entry);
                s.ActiveServerId ??= entry.Id;
                s.ServerInstallDir = installDir; // legacy compat

                s.WindrosePlusActiveByServer[installDir] = optedIn;
                s.WindrosePlusOptInStateByServer[installDir] = optedIn ? OptInState.OptedIn : OptInState.OptedOut;
                if (optedIn)
                {
                    s.WindrosePlusRconPasswordByServer[installDir] = rcon;
                    s.WindrosePlusDashboardPortByServer[installDir] = port;
                    if (steamId is not null)
                        s.WindrosePlusAdminSteamIdByServer[installDir] = steamId;
                }
            }, _cts.Token);

            InstallCompleted = true;
            _toasts.Success(Loc.Get("Installation.Complete"));
        }
        catch (System.OperationCanceledException)
        {
            CurrentPhase = Loc.Get("Installation.Cancelled");
        }
        catch (WindrosePlusOfflineException)
        {
            WizardError = Loc.Get("Error.WindrosePlus.NoInternet");
            HasWizardError = true;
            _toasts.Error(WizardError);
        }
        catch (Exception ex)
        {
            WizardError = ErrorMessageHelper.FriendlyMessage(ex);
            HasWizardError = true;
            _toasts.Error(WizardError);
        }
        finally
        {
            IsInstalling = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void FinishWizard()
    {
        IsAddingServer = false;
        RefreshServerCards();
        // Ensure sidebar selection matches this page
        _nav.NavigateTo(this);
    }

    [RelayCommand]
    private async Task InstallWindrosePlus(string id)
    {
        var entry = _settings.Current.Servers.FirstOrDefault(s => s.Id == id);
        if (entry is null) return;

        if (!Directory.Exists(Path.Combine(entry.InstallDir, "R5")))
        {
            _toasts.Error(Loc.Get("Error.ServerNotInstalled"));
            return;
        }

        var retrofitVm = new RetrofitBannerViewModel(entry.InstallDir, _wplus, _wplusApi, _settings, _toasts);
        retrofitVm.StateChanged += RefreshServerCards;
        await retrofitVm.OpenRetrofitDialogCommand.ExecuteAsync(null);
        retrofitVm.StateChanged -= RefreshServerCards;
        RefreshServerCards();
    }

    [RelayCommand]
    private async Task UpdateWindrosePlus(string id)
    {
        var entry = _settings.Current.Servers.FirstOrDefault(s => s.Id == id);
        if (entry is null) return;
        var card = ServerCards.FirstOrDefault(c => c.Id == id);

        // Safety: server must be stopped — we're overwriting DLLs.
        if (entry.Id == _settings.Current.ActiveServerId &&
            _process.Status is ServerStatus.Running or ServerStatus.Starting)
        {
            _toasts.Warning(Loc.Get("Server.WindrosePlus.Update.StopFirst"));
            return;
        }

        if (card is not null) card.IsUpdatingWindrosePlus = true;
        try
        {
            _toasts.Info(Loc.Format("Server.WindrosePlus.Update.Starting", entry.Name));
            await _wplus.InstallAsync(entry.InstallDir, null, CancellationToken.None);
            _toasts.Success(Loc.Format("Server.WindrosePlus.Update.Done", entry.Name));
            await _wplusUpdate.CheckAsync();
        }
        catch (WindrosePlusOfflineException)
        {
            _toasts.Error(Loc.Get("Error.WindrosePlus.NoInternet"));
        }
        catch (Exception ex)
        {
            _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex));
        }
        finally
        {
            if (card is not null) card.IsUpdatingWindrosePlus = false;
            RefreshServerCards();
        }
    }

    [RelayCommand]
    private async Task UpdateAllWindrosePlus()
    {
        var result = _wplusUpdate.LastResult;
        if (result is null || !result.AnyUpdate) return;

        var targets = result.Servers.Where(x => x.HasUpdate).ToList();
        if (targets.Count == 0) return;

        // Skip the active server if it's currently running (user must stop it first).
        var activeId = _settings.Current.ActiveServerId;
        var activeRunning = _process.Status is ServerStatus.Running or ServerStatus.Starting;

        int ok = 0, skipped = 0, failed = 0;
        foreach (var t in targets)
        {
            if (t.ServerId == activeId && activeRunning)
            {
                _toasts.Warning(Loc.Format("Server.WindrosePlus.Update.SkippedRunning", t.ServerName));
                skipped++;
                continue;
            }
            var card = ServerCards.FirstOrDefault(c => c.Id == t.ServerId);
            if (card is not null) card.IsUpdatingWindrosePlus = true;
            try
            {
                await _wplus.InstallAsync(t.InstallDir, null, CancellationToken.None);
                ok++;
            }
            catch (Exception ex)
            {
                failed++;
                _toasts.Error(Loc.Format("Server.WindrosePlus.Update.Failed", t.ServerName, ErrorMessageHelper.FriendlyMessage(ex)));
            }
            finally
            {
                if (card is not null) card.IsUpdatingWindrosePlus = false;
            }
        }

        await _wplusUpdate.CheckAsync();
        _toasts.Info(Loc.Format("Server.WindrosePlus.Update.BatchSummary", ok, failed, skipped));
        RefreshServerCards();
    }

    [RelayCommand]
    private async Task SelectServer(string id)
    {
        await _settings.SelectServerAsync(id);
        RefreshServerCards();
    }

    [RelayCommand]
    private void OpenFolder(string id)
    {
        var entry = _settings.Current.Servers.FirstOrDefault(s => s.Id == id);
        if (entry is null || string.IsNullOrWhiteSpace(entry.InstallDir)) return;
        if (!Directory.Exists(entry.InstallDir))
        {
            _toasts.Warning(Loc.Get("Error.ServerNotInstalled"));
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = entry.InstallDir,
                UseShellExecute = true,
                Verb = "open",
            });
        }
        catch (Exception ex) { _toasts.Error(ex.Message); }
    }

    [RelayCommand]
    private void OpenLiveMap(string id)
    {
        var entry = _settings.Current.Servers.FirstOrDefault(s => s.Id == id);
        if (entry is null) return;
        var port = _settings.Current.WindrosePlusDashboardPortByServer.GetValueOrDefault(entry.InstallDir, 0);
        if (port <= 0)
        {
            _toasts.Warning(Loc.Get("Server.LiveMap.Unavailable"));
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://localhost:{port}/livemap",
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { _toasts.Error(ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteServer(string id)
    {
        var entry = _settings.Current.Servers.FirstOrDefault(s => s.Id == id);
        if (entry is null) return;

        var owner = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;

        var (confirmed, deleteFiles) = await WindroseServerManager.App.Views.Dialogs.DeleteServerDialog.ShowAsync(owner, entry.Name);
        if (!confirmed) return;

        var installDir = entry.InstallDir;
        await _settings.UpdateAsync(s =>
        {
            s.Servers.RemoveAll(x => x.Id == id);
            if (s.ActiveServerId == id)
                s.ActiveServerId = s.Servers.Count > 0 ? s.Servers[0].Id : null;
        });

        if (deleteFiles && Directory.Exists(installDir))
        {
            try { Directory.Delete(installDir, recursive: true); }
            catch (Exception ex) { _toasts.Error(ex.Message); }
        }

        _toasts.Info(deleteFiles ? Loc.Get("Server.DeletedWithFiles") : Loc.Get("Server.Deleted"));
        RefreshServerCards();
    }
}
