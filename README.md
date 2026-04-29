# Windrose Server Manager

**Native desktop manager for [Windrose](https://store.steampowered.com/app/4129620/Windrose_Dedicated_Server/) dedicated servers — with deep [Windrose+](https://github.com/HumanGenome/WindrosePlus) integration for player management, live map, and a configuration editor.**

A Windows desktop app (Avalonia / .NET 9) that bundles SteamCMD setup, server control, configuration editing, mod management, backups, firewall rules, and app-update checks into one clean UI — and adds the full Windrose+ feature stack on top: player list with kick/ban, events log, live map, health checks and Windrose+ update notifications.

**Status: Stable · v1.2.0**

![Version](https://img.shields.io/badge/version-1.2.4-success)
![License](https://img.shields.io/badge/license-MIT-blue)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey)
![.NET](https://img.shields.io/badge/.NET-9-512BD4)
![Built on Windrose+](https://img.shields.io/badge/built%20on-Windrose%2B-orange)
![Language](https://img.shields.io/badge/UI-English%20%7C%20German-blueviolet)

> The UI ships in **English and German** with auto-detection from the Windows language setting (screenshots happen to show the German UI).

---

## Built on Windrose+

The headline feature in v1.2.0 — and most of the player-management UI you'll see in this manager — is powered by **[Windrose+](https://github.com/HumanGenome/WindrosePlus)**, an MIT-licensed mod by **HumanGenome** that adds the HTTP API, RCON, events log, and live map that Windrose itself doesn't expose.

The manager just provides a friendly UI on top: the install wizard offers Windrose+ during server setup, you click yes, and everything below works — no manual mod install, no JSON editing, no RCON-password juggling.

If you'd rather stay vanilla, **Windrose+ is fully optional**. Every WindrosePlus-powered view shows an empty state explaining what you'd unlock if you opted in. You can also enable it later from Settings or via a one-click banner on existing servers.

A huge thank-you to **HumanGenome** for building Windrose+, keeping the API stable, and explicitly green-lighting this integration. Please [star the Windrose+ repo](https://github.com/HumanGenome/WindrosePlus) — they're doing the heavy lifting.

---

## Features

### Player management (with Windrose+)
- **Player list** — see who's online, with one-click **kick**, **ban** and **broadcast**.
- **Events log** — every join and leave with timestamps and a live filter.
- **Live map** — opens in your browser, shows real-time player positions on the world.
- **Configuration editor** — XP rates, loot multipliers, RCON, harvest yields, all grouped and validated. No more hand-editing JSON.
- **Health banner** on the dashboard if something is wrong, with a one-click "Report" button that prefills a Windrose+ GitHub issue with the right context.
- **Update checks** for new Windrose+ releases, with one-click update for one or all servers.

### Multi-server support
- Manage as many Windrose servers as you want from one app.
- Each server gets its own card on the Server page with start/stop, open-folder, jump-to-live-map, auto-start toggle.
- Per-server config, RCON password, port and state — fully isolated.

### Server installation & adoption
- **Fresh install via SteamCMD** (App-ID `4129620`, anonymous login) with live download progress.
- **Adopt an existing install** — point the wizard at a folder where you already installed Windrose via SteamCMD, and it skips the download and just registers the server.
- Path validation (no UNC, no Program Files, no special chars).
- Update check via Steam build ID.
- Safeguards: no double-registering the same folder, no overwriting Windrose+ while a server is running.

### Server control & automation
- **Start / Graceful-Stop / Force-Kill / Restart**
- **Auto-restart on crash** (opt-in)
- **Scheduled restarts** per weekday with configurable warning toast
- **Threshold-based restarts** — high RAM usage or max uptime
- **Session history** of all starts / stops / crashes with duration
- **Live log** with colour coding (errors red, warnings orange)

### Configuration
- Form-based editor for `ServerDescription.json` and `WorldDescription.json`
- Manage multiple worlds (create, activate, delete)
- Invite code generator (re-roll)
- World parameters (difficulty, mob health/damage, ship stats, …)
- Password protection with masked input

### Mod management
- **Drag & drop** install from `.pak`, `.zip`, or `.7z` (7z via SharpCompress)
- **Automatic grouping** — mods coming from a single archive appear as one expandable mod card
- **Enable/disable** via `.pak` ↔ `.pak.disabled` rename (companion `.ucas`/`.utoc` files move with it)
- **"Open on Nexus"** link for any mod whose Nexus ID was auto-detected from the archive filename or linked manually

### Backups
- One-click backup of `ServerDescription.json` + worlds + mods
- Scheduled backups (daily / weekly)
- Safe-restore preview before overwriting
- Configurable retention

### System integration
- Tray icon with quick start/stop
- Windows firewall rule (`netsh advfirewall`, prompts for admin)
- HKCU Run-Key autostart
- App self-update via GitHub Releases (manual install)
- Optional console-log launch arg

### Design
- Native dark UI (Avalonia 12 + Semi.Avalonia + custom navy/amber brand layer)
- Maritime/pirate flavour, but functional first
- Mica backdrop on Windows 11
- Custom window chrome with Windows 11 rounded corners

---

## Screenshots

### Dashboard — status, health banner, metrics at a glance
![Dashboard](Screenshots/01-dashboard.png)

### Installation — SteamCMD or adopt an existing install
![Installation](Screenshots/02-installation.png)

### Log & Automation — scheduled restarts and live log
![Log & Automation](Screenshots/03-log.png)

### Configuration — server and world parameters
![Configuration](Screenshots/04-configuration.png)

### Mods — drag & drop + "Open on Nexus" link
![Mods](Screenshots/05-mods.png)

### Backups — scheduled + safe restore
![Backups](Screenshots/06-backups.png)

### Settings — language, firewall, autostart, app-update, Windrose+
![Settings](Screenshots/07-settings.png)

> Screenshots reflect v1.1.0 — some Windrose+ panels (player list, events, live map, health banner) added in v1.2.0 will be refreshed shortly.

---

## System Requirements

- **Windows 10 (1809+)** or **Windows 11**
- ~300 MB for the app + SteamCMD
- 10–20 GB for the Windrose server itself
- Internet access (for SteamCMD downloads, Windrose+ download from GitHub on opt-in, and the app-update check)

No separate .NET install required — the self-contained build ships everything.

## Install

### Option A: Installer (recommended)
1. Download `WindroseServerManager-Setup-1.2.0.exe` from the [Releases page](https://github.com/ManuelStaggl/WindroseServerManager/releases)
2. Run the installer, follow the prompts
3. Launch from the Start Menu

### Option B: Portable ZIP
1. Download `WindroseServerManager-1.2.0-portable.zip`
2. Extract anywhere
3. Run `WindroseServerManager.exe`

### Option C: Build from source
```powershell
git clone https://github.com/ManuelStaggl/WindroseServerManager
cd WindroseServerManager
dotnet build src/WindroseServerManager.App
# or a release build:
.\scripts\build-release.ps1
```

## First Run

1. **Dashboard** opens with an onboarding card.
2. **Server** → "Add server" → pick a folder.
   - Empty folder → fresh install via SteamCMD.
   - Folder already contains a Windrose install → wizard adopts it (no download).
3. **Windrose+ opt-in step** → keep the default ("Install Windrose+") for the full feature set, or skip if you want vanilla.
4. **Server Control** → "Start".
5. **Settings** → add firewall rule (accept admin prompt).
6. Optional: **Mods** → drop `.pak` / `.zip` / `.7z` files onto the page.

## Paths

| Purpose | Path |
|---|---|
| Settings | `%AppData%\WindroseServerManager\settings.json` |
| App logs | `%LocalAppData%\WindroseServerManager\logs\app-YYYYMMDD.log` (rolling, 7 days) |
| Crash logs | `%LocalAppData%\WindroseServerManager\crashes\crash-*.txt` |
| SteamCMD | `%LocalAppData%\WindroseServerManager\steamcmd\` |
| Windrose+ cache | `%LocalAppData%\WindroseServerManager\cache\windroseplus\` |
| Backups (default) | `%LocalAppData%\WindroseServerManager\backups\` |
| Server install | user-chosen |
| Mods | `<ServerInstallDir>\R5\Content\Paks\~mods\` |
| Windrose+ files | `<ServerInstallDir>\windrose_plus\` (LICENSE preserved here) |

## Nexus Mods — How linking works

The app **does not talk to the Nexus API**. It does not store an API key, does not download mods for you, and does not poll Nexus for updates. Mods are always downloaded manually from nexusmods.com — that is the normal Nexus workflow.

What the app *does* do:

- When you drop a Nexus download archive onto the Mods page, the mod ID is extracted from the filename (Nexus's standard naming pattern: `Name-{modId}-{version}-{timestamp}.zip`) and remembered next to the installed `.pak` in a small side-car `.pak.meta.json` file.
- A **"Open on Nexus"** button on each linked mod card jumps straight to `https://www.nexusmods.com/windrose/mods/{id}` in your browser. That's the single network touch — and it's just launching a URL.
- You can also link any installed mod manually by pasting a Nexus URL or mod ID.

To check whether a mod has an update: click "Open on Nexus" and look at the page. The app will not nag you.

## Project Layout

```
WindroseServerManager/
├── src/
│   ├── WindroseServerManager.Core/    Service layer (platform-agnostic)
│   └── WindroseServerManager.App/     Avalonia desktop UI
├── tests/
│   └── WindroseServerManager.Core.Tests/
├── scripts/
│   ├── publish.ps1              Self-contained single-file build
│   ├── build-release.ps1        Release + ZIP + optional installer
│   └── installer.iss            Inno Setup template
├── Screenshots/                 README images
└── artifacts/                   Build output
```

## Stack

- **.NET 9** · **Avalonia 12** · Semi.Avalonia (with custom Navy/Amber brand layer)
- **CommunityToolkit.Mvvm** · Microsoft.Extensions.Hosting / DI
- **Serilog** (file sink, daily rolling)
- **SharpCompress** (7z support)
- Windows-specific: tray icon, HKCU Run-Key, Netsh firewall, DwmSetWindowAttribute

## Tests

Unit tests in `tests/WindroseServerManager.Core.Tests` (xUnit):

```powershell
dotnet test
```

Covers: mod install/uninstall/enable/disable, side-car metadata I/O, Nexus URL parser, archive filename parser, ServerDescription round-trip, invite code generator, AppSettings defaults, world parameter catalog, Windrose+ install (license preservation, atomicity, version marker, offline fallback), launcher resolution, health check, events log parser, editor config schema validation, report URL builder.

## Credits

- **[Windrose+](https://github.com/HumanGenome/WindrosePlus)** by **HumanGenome** — MIT-licensed mod that powers all player-management features in this app. Bundled at install time via fresh download from GitHub when you opt in; LICENSE preserved next to the mod, attribution in the About dialog. Coordination with HumanGenome confirmed Windrose+ is OK to bundle this way and the API surfaces (HTTP endpoints, RCON, events log, `windrose_plus.json`) are stable across point releases.
- The Windrose admin community on Reddit — every "is there a way to kick a player?" thread shaped what this app became.

## License

[MIT](LICENSE) — community app, not a commercial product.

**Disclaimer:** Windrose Server Manager is an unofficial community tool. Windrose is a trademark of its respective owners. Not affiliated with or endorsed by Red Rook Games or Nexus Mods.

## Contributing

Pull requests, issues, and feature requests are welcome. For larger changes, please open an issue first to discuss the approach.
