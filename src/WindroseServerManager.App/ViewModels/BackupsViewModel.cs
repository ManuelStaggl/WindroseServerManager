using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class BackupsViewModel : ViewModelBase
{
    private readonly IBackupService _backup;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _proc;
    private readonly IToastService _toasts;

    public ObservableCollection<BackupInfo> Backups { get; } = new();

    [ObservableProperty] private BackupInfo? _selected;
    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private int _autoBackupIntervalMinutes;
    [ObservableProperty] private int _maxBackupsToKeep;
    [ObservableProperty] private string _backupDir = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public BackupsViewModel(IBackupService backup, IAppSettingsService settings, IServerProcessService proc, IToastService toasts)
    {
        _backup = backup;
        _settings = settings;
        _proc = proc;
        _toasts = toasts;

        AutoBackupEnabled = settings.Current.AutoBackupEnabled;
        AutoBackupIntervalMinutes = settings.Current.AutoBackupIntervalMinutes;
        MaxBackupsToKeep = settings.Current.MaxBackupsToKeep;
        BackupDir = settings.Current.BackupDir;
        Refresh();
    }

    [RelayCommand]
    private async Task PickBackupDirAsync()
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var picks = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Backup-Ordner wählen",
            AllowMultiple = false,
        });
        if (picks.Count == 0) return;
        var path = picks[0].Path.LocalPath;
        BackupDir = path;
        await _settings.UpdateAsync(s => s.BackupDir = path);
        _toasts.Success("Backup-Ordner aktualisiert.");
        Refresh();
    }

    private static Window? GetOwnerWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow
            : null;
    }

    [RelayCommand]
    private void OpenBackupDir()
    {
        try
        {
            var dir = _backup.GetBackupDir();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex) { _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex)); }
    }

    [RelayCommand]
    private void Refresh()
    {
        Backups.Clear();
        try
        {
            foreach (var b in _backup.ListBackups()) Backups.Add(b);
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (_proc.Status is ServerStatus.Running or ServerStatus.Starting)
        {
            var owner = GetOwnerWindow();
            if (owner is not null)
            {
                var confirmed = await ConfirmDialog.ShowAsync(
                    owner,
                    "Server läuft",
                    "Ein Backup während laufendem Server kann inkonsistent werden (RocksDB schreibt gerade). Fortfahren?",
                    confirmLabel: "Backup trotzdem erstellen",
                    danger: true);
                if (!confirmed) return;
            }
        }

        IsBusy = true;
        try
        {
            var b = await _backup.CreateBackupAsync(isAutomatic: false);
            if (b is null) { ErrorMessage = "Saves-Ordner nicht gefunden."; _toasts.Error("Saves-Ordner nicht gefunden."); }
            else { StatusMessage = $"Backup erstellt: {b.FileName}"; _toasts.Success($"Backup erstellt: {b.FileName}"); }
            Refresh();
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        var fileName = Selected.FileName;

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                "Backup löschen?",
                $"Möchten Sie das Backup '{fileName}' wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.",
                confirmLabel: "Löschen",
                danger: true);
            if (!confirmed) return;
        }

        try
        {
            _backup.DeleteBackup(fileName);
            StatusMessage = $"Gelöscht: {fileName}";
            _toasts.Success($"Gelöscht: {fileName}");
            Refresh();
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Selected is null) return;
        if (_proc.Status is ServerStatus.Running or ServerStatus.Starting)
        {
            _toasts.Warning("Server läuft — bitte zuerst stoppen.");
            return;
        }

        var fileName = Selected.FileName;

        var owner = GetOwnerWindow();
        if (owner is not null)
        {
            var confirmed = await ConfirmDialog.ShowAsync(
                owner,
                "Backup wiederherstellen?",
                $"Das Backup '{fileName}' wird die aktuellen Saves überschreiben. Vor der Wiederherstellung wird automatisch ein Safety-Snapshot der aktuellen Saves erstellt.",
                confirmLabel: "Wiederherstellen",
                danger: false);
            if (!confirmed) return;
        }

        IsBusy = true;
        try
        {
            await _backup.RestoreBackupAsync(fileName);
            StatusMessage = $"Wiederhergestellt: {fileName}";
            _toasts.Success($"Wiederhergestellt: {fileName}");
            Refresh();
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await _settings.UpdateAsync(s =>
        {
            s.AutoBackupEnabled = AutoBackupEnabled;
            s.AutoBackupIntervalMinutes = Math.Max(5, AutoBackupIntervalMinutes);
            s.MaxBackupsToKeep = Math.Max(1, MaxBackupsToKeep);
        });
        StatusMessage = "Einstellungen gespeichert.";
        _toasts.Success("Einstellungen gespeichert.");
    }
}
