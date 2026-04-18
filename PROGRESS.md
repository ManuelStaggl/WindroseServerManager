# Windrose Server Manager — Progress

**Status: Production-Ready · v1.0.0**

## Feature-Checkliste

### Core
- [x] Avalonia Desktop UI (.NET 9, Semi.Avalonia)
- [x] MVVM via CommunityToolkit.Mvvm
- [x] DI via Microsoft.Extensions.Hosting
- [x] Serilog (File-Sink, rolling daily, 7 Tage Retention)
- [x] Globaler Crash-Handler (`%LocalAppData%\WindroseServerManager\crashes\`)
- [x] Atomic Settings-Write (`.tmp` + `File.Move`)

### UI
- [x] Dark/Light Theme (Amber-Akzent)
- [x] Deutsch + Englisch
- [x] Custom Window-Chrome (Drag, Min/Max/Close)
- [x] Navigation-Sidebar
- [x] Toast-Notifications
- [x] Dashboard mit Live-Metriken
- [x] Onboarding-Card (First-Run)
- [x] Crash-Warning-Card (letzte 7 Tage)
- [x] About-Dialog (Version, Links, Lizenz)

### Server-Management
- [x] SteamCMD Auto-Install
- [x] Windrose Server Install/Update via SteamCMD
- [x] Start / Graceful-Stop / Force-Kill
- [x] Auto-Restart bei Crash
- [x] Live stdout/stderr
- [x] Launch-Args konfigurierbar (Log, Extra-Args)

### Konfiguration
- [x] ServerDescription.json Editor
- [x] WorldDescription.json Editor
- [x] Invite-Code-Generator
- [x] Active-World-Selection

### Backups
- [x] Manuelles Backup (ZIP)
- [x] Auto-Backup (N Minuten, Minimum 5)
- [x] Retention (MaxBackupsToKeep)
- [x] Restore mit Safety-Snapshot
- [x] Confirm-Dialog bei destruktiven Aktionen

### System-Integration
- [x] Windows Firewall Ein-Klick-Regel
- [x] Täglicher Auto-Restart (Scheduler)
- [x] Update-Check vs. Steam-Build-ID
- [x] Tray-Icon (Show/Start/Stop/Quit)
- [x] Autostart via HKCU Run-Key
- [x] `--tray` / `--minimized` Launch-Arg

### Release-Infrastruktur
- [x] Versionsnummer in csproj (1.0.0)
- [x] `scripts/publish.ps1` — Self-Contained Single-File
- [x] `scripts/build-release.ps1` — ZIP + optional Inno-Installer
- [x] `scripts/installer.iss` — Inno-Setup Template
- [x] README.md (Public-Release-tauglich)
- [x] LICENSE (MIT)

## Offene Punkte (Post-1.0)

- [ ] RCON/Live-Player-Liste (wartet auf Windrose-native RCON)
- [ ] Remote Server Management (mehrere Server in einem Panel)
- [ ] Screenshots in `docs/screenshots/`
- [ ] GitHub-Repo-URL in Code (AboutDialog) + README ersetzen
- [ ] Code-Signing-Zertifikat für Installer
