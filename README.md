# Windrose Server Manager

**Dedicated Server Manager für [Windrose](https://store.steampowered.com/app/3041230/Windrose/)** — eine native Windows-Desktop-App (Avalonia / .NET 9), die SteamCMD, Server-Steuerung, Konfiguration, Backups, Firewall-Regeln und Update-Checks in einer schlanken UI bündelt.

**Status: Beta · v0.9.5**

![Version](https://img.shields.io/badge/version-0.9.5-orange)
![License](https://img.shields.io/badge/license-MIT-blue)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey)

## Features

- **Dashboard** — Server-Status, Live-Host-Metriken (CPU/RAM/Disk), Server-Prozess-Metriken, aktive Welt, Invite-Code-Kopieren
- **Installation** — Vollautomatisch via SteamCMD (App-ID `4129620`, anonymous), mit Live-Log und Update-Check
- **Server-Steuerung** — Start, Graceful-Stop, Force-Kill, Auto-Restart bei Crash, Live stdout/stderr
- **Konfigurations-Editor** — `ServerDescription.json` + `WorldDescription.json` formularbasiert, Invite-Code-Generator
- **Backups** — ZIP von `R5/Saved/`, manuell oder geplant (N Minuten), Retention, Safe-Restore mit Safety-Snapshot
- **Täglicher Auto-Restart** — konfigurierbare Uhrzeit
- **Firewall** — Ein-Klick-Regel (Admin), UDP 7777 + 7778
- **Update-Check** — periodisch vs. Steam-Build-ID
- **Tray-Icon** — Start/Stop/Show/Quit aus der Taskleiste
- **Autostart** — HKCU Run-Key für Windows-Anmeldung
- **Crash-Logger** — Alle Abstürze werden nach `%LocalAppData%\WindroseServerManager\crashes\` geschrieben
- **Light/Dark Theme** (Amber-Akzent) · Deutsch + Englisch

## Systemanforderungen

- Windows 10 (1809+) oder Windows 11
- ~300 MB Platz für die App + SteamCMD, + Platz für den Windrose-Server (~10-20 GB)
- Internet-Zugang für SteamCMD-Downloads

Keine separate .NET-Installation nötig — Self-Contained-Build bringt alles mit.

## Installation

### Option A: Installer (empfohlen)
1. `WindroseServerManager-Setup-x.y.z.exe` von der Releases-Seite herunterladen
2. Installer ausführen, Anweisungen folgen
3. App aus dem Startmenü oder von der Desktop-Verknüpfung starten

### Option B: Portable ZIP
1. `WindroseServerManager-x.y.z.zip` herunterladen
2. Entpacken in einen Ordner deiner Wahl
3. `WindroseServerManager.exe` ausführen

### Option C: Aus Quelle bauen
```powershell
git clone https://github.com/ManuelStaggl/WindroseServerManager
cd WindroseServerManager
dotnet build src/WindroseServerManager.App
# oder Release-Build:
.\scripts\build-release.ps1
```

## Erster Start

1. **Dashboard** öffnet sich mit einer Onboarding-Card
2. **Installation** → "Server installieren" (lädt SteamCMD + Windrose Server)
3. **Konfiguration** → Welt wählen, Server-Namen setzen, optional Invite-Code generieren
4. **Server-Steuerung** → "Start"
5. Firewall-Regel unter "Einstellungen" → "Regel hinzufügen" (Admin-Prompt akzeptieren)

## Pfade

| Zweck | Pfad |
|---|---|
| Einstellungen | `%AppData%\WindroseServerManager\settings.json` |
| App-Logs | `%LocalAppData%\WindroseServerManager\logs\app-YYYYMMDD.log` (rolling, 7 Tage) |
| Crash-Logs | `%LocalAppData%\WindroseServerManager\crashes\crash-*.txt` |
| SteamCMD | `%LocalAppData%\WindroseServerManager\steamcmd\` |
| Backups (Default) | `%LocalAppData%\WindroseServerManager\backups\` |
| Server-Installation | vom User wählbar — Default `%LocalAppData%\WindroseServerManager\server\` |

## Projekt-Struktur

```
WindroseServerManager/
├── src/
│   ├── WindroseServerManager.Core/    Service-Layer (plattformunabhängig)
│   └── WindroseServerManager.App/     Avalonia Desktop UI
├── scripts/
│   ├── publish.ps1              Self-contained Single-File Build
│   ├── build-release.ps1        Release + ZIP + optional Installer
│   └── installer.iss            Inno-Setup Template
└── artifacts/                   Build-Output
```

## Stack

- **.NET 9** · **Avalonia 12** · Semi.Avalonia (Fluent-Style)
- **CommunityToolkit.Mvvm** · Microsoft.Extensions.Hosting / DI
- **Serilog** (File-Sink, daily-rolling)
- Windows-Features: Tray-Icon, HKCU Run-Key, Netsh Firewall

## Screenshots

> _Screenshots werden ergänzt — siehe `docs/screenshots/` nach Community-Release._

## Lizenz

[MIT](LICENSE) — Private Community-App, kein kommerzielles Produkt.

**Disclaimer:** Windrose Server Manager ist ein inoffizielles Community-Tool. Windrose ist eine Marke von Red Rook Games. Nicht verbunden mit oder unterstützt von den Entwicklern.

## Mitwirken

Pull Requests, Issues und Feature-Requests willkommen. Bitte vor größeren Änderungen ein Issue eröffnen um das Konzept zu diskutieren.
