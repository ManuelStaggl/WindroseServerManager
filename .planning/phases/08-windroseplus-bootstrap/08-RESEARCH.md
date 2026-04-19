# Phase 8: WindrosePlus Bootstrap - Research

**Researched:** 2026-04-19
**Domain:** GitHub-sourced mod bootstrap, atomic file installation, launcher multiplexing (.NET 9 / WPF / Avalonia)
**Confidence:** HIGH (stack + patterns) / MEDIUM (upstream archive layout — verified via README/install.ps1 fetch but not by physically inspecting WindrosePlus.zip v1.0.6)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Cache & Version Strategy**
- Cache location: `%LocalAppData%\WindroseServerManager\cache\windroseplus\` — single shared cache across all servers
- Version selection: Always latest stable release from GitHub Releases API (no prereleases, no per-server pinning in v1.2 — pinning belongs to UPGRADE-01 in v1.3)
- Cache semantics: Every install attempts a live GitHub fetch; cache is only consulted as offline fallback (spec-aligned with WPLUS-01)
- First install offline: Hard-fail with a clear error message and a retry button — no silent degradation, no partial server creation

**UE4SS Handling**
- Source: UE4SS comes entirely from the WindrosePlus GitHub release asset (HumanGenome bundles it). We do not fetch UE4SS separately and do not maintain a compatibility matrix
- Preexisting install: Binaries are overwritten on re-install; user config files (WindrosePlus.json / .ini) are preserved
- Extraction integrity: Verify archive against the SHA published by the GitHub Releases API asset. Extract into a temp directory, then atomic-move into the server directory — the server stays functional until extraction is complete

**⚠️ RESEARCH CONFLICT — MUST be resolved before planning:** The CONTEXT assumption that "UE4SS comes entirely from the WindrosePlus GitHub release asset" does **not** match the upstream v1.0.6 release. The `WindrosePlus.zip` asset ships **without** UE4SS; the `install.ps1` script inside the zip downloads UE4SS from its own GitHub release at install time. See §"Upstream Archive Layout" below. The planner must either (a) accept fetching UE4SS from its upstream as part of the install flow, or (b) bundle a pinned UE4SS zip alongside the WindrosePlus zip in our cache, or (c) coordinate with HumanGenome to publish a self-contained release. Discretion area — but **not** a silent decision.

**Launcher Switch & Persistence**
- Flag storage: The per-server "WindrosePlus active" flag lives in the existing per-server `ServerInstallInfo` / AppSettings structure. Migration rule: servers without the flag (carried over from v1.0/v1.1) are treated as opted-out until the Phase 9 retrofit dialog explicitly sets them
- Missing .bat fallback: If the flag says "active" but `StartWindrosePlusServer.bat` is missing, emit a Toast + log warning and launch `WindroseServer.exe`. No hard block — the Phase 10 health banner will surface the inconsistency
- Integration with ServerProcessService: `ServerProcessService` stays a dumb launcher and queries `WindrosePlusService` for the correct start-file path + args. All WindrosePlus-specific knowledge is encapsulated in the new service

**Install Contract & Version Tracking**
- Install API: `async Task InstallAsync(server, IProgress<InstallProgress>, CancellationToken)` — mirrors the existing `SteamCmdService` pattern so the Phase 9 wizard and retrofit dialog share the progress surface
- Version marker: Write a `.wplus-version` file (or equivalent) into the install output containing the GitHub tag that was installed. Used by Phase 10's health report and as the foundation for v1.3's UPGRADE-01 — does not depend on WindrosePlus exposing its own `/version` endpoint

**About-Dialog License Exposure (WPLUS-03)**
- Dedicated "Third-Party Licenses" section in the About dialog
- WindrosePlus MIT text rendered inline + link to `https://github.com/HumanGenome/WindrosePlus`
- The LICENSE file is also copied into the server install output (bundle requirement, independent of the About UI)
- Product name "WindrosePlus" appears verbatim everywhere — never rebranded

### Claude's Discretion
- Exact folder structure inside `%LocalAppData%\...\cache\windroseplus\` (flat vs per-version subdirs)
- Concrete `InstallProgress` stages / percentages
- GitHub API client: reuse existing HttpClient/UpdateCheckService infra or new thin wrapper
- Exact error-message copy for offline-fail and missing-.bat toast (follow existing ErrorMessageHelper patterns)

### Deferred Ideas (OUT OF SCOPE)
- Version pinning per server (UPGRADE-01, v1.3)
- Automatic background upgrade check (UPGRADE-01, v1.3)
- Prerelease channel opt-in
- UE4SS version independence (bundling our own UE4SS separately if HumanGenome ever drops it from their release)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| WPLUS-01 | App fetches latest WindrosePlus release from GitHub Releases API and caches archive locally as offline-install fallback | GitHub REST endpoint `/repos/HumanGenome/WindrosePlus/releases/latest` — verified returning `WindrosePlus.zip` asset with SHA256 digest field (see §"GitHub Releases API"). Cache pattern mirrors existing `%LocalAppData%\WindroseServerManager\steamcmd\` layout. |
| WPLUS-02 | WindrosePlus install extracts into active server's game-binaries directory and produces working UE4SS + WindrosePlus payload | Archive extracts to server root; UE4SS proxy DLL (dwmapi.dll) lands in `R5\Binaries\Win64\` per install.ps1 (see §"Upstream Archive Layout"). Atomic install pattern via temp-extract → SHA verify → `ReplaceFile`/`File.Move` (§"Atomic Install on Windows"). |
| WPLUS-03 | WindrosePlus LICENSE (MIT, HumanGenome) bundled with install and shown in About dialog; name "WindrosePlus" never rebranded | Upstream LICENSE at repo root (MIT). Copy into install output + embed raw text as assembly resource for About dialog. |
| WPLUS-04 | On server launch, uses `StartWindrosePlusServer.bat` when WindrosePlus active, `WindroseServer.exe` when opted out | `ServerProcessService.StartAsync` currently hardcodes `ServerInstallService.FindServerBinary(dir)` — add indirection through `IWindrosePlusService.ResolveLauncher(serverId)` returning `(filename, args)`. |
</phase_requirements>

## Summary

Phase 8 delivers a self-contained `WindrosePlusService` that (a) calls the GitHub Releases API to find the latest stable `WindrosePlus.zip`, (b) downloads and SHA-256-verifies it against the asset's `digest` field (GitHub auto-populates this for all assets published after June 2025 — current v1.0.6 has one), (c) extracts the archive into a temporary directory under `%LocalAppData%\WindroseServerManager\cache\windroseplus\`, (d) atomically copies vendor files into the server directory while preserving user config (`windrose_plus.json`, `windrose_plus.ini`, `Mods/`), and (e) writes a `.wplus-version` marker so later phases can show the installed tag.

The shape of the service mirrors `SteamCmdService` exactly — `IProgress<InstallProgress>` + `CancellationToken` + `SemaphoreSlim` for concurrency — so the Phase 9 wizard/retrofit dialog can drop it into their existing progress UI. GitHub API calls reuse the exact HTTP pattern already in `AppUpdateService.cs` (same User-Agent header contract, same `application/vnd.github+json` Accept header, same 60 req/h unauthenticated budget).

**Primary recommendation:** Build `WindrosePlusService` as a sealed class in `WindroseServerManager.Core.Services` that mirrors `SteamCmdService`'s public shape, uses `IHttpClientFactory` with a dedicated named client `"github"` (to match existing HttpClient registrations), and exposes three methods: `FetchLatestAsync` (API + cache), `InstallAsync` (extract + atomic move), `ResolveLauncher` (for `ServerProcessService`).

**⚠️ Blocking issue for planner:** Upstream `WindrosePlus.zip` (v1.0.6) does **not** contain UE4SS binaries — it ships `install.ps1` that downloads UE4SS at install time. The locked decision "UE4SS comes entirely from the WindrosePlus GitHub release asset" is factually incorrect for the current release. See §"Upstream Archive Layout" and §"Open Questions" Q1.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Compression` | .NET 9 BCL | ZIP extraction | Already used by `SteamCmdService.ZipFile.ExtractToDirectory` — same pattern, no new dep |
| `System.Net.Http` via `IHttpClientFactory` | .NET 9 BCL | GitHub API calls + asset download | Already registered in DI; `AppUpdateService` and `SteamCmdService` both use it |
| `System.Text.Json` | .NET 9 BCL | Parse GitHub Releases JSON | Already used by `AppUpdateService.cs` — mirror exactly |
| `System.Security.Cryptography.SHA256` | .NET 9 BCL | Asset digest verification | `SHA256.HashData(stream)` since .NET 5 — zero ceremony |
| `Microsoft.Extensions.Logging` | 9.x | Structured logging | Already in every service |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `CommunityToolkit.Mvvm` | 8.4.0 (pinned, per project) | ViewModel wiring in About dialog | Only for the license-view bits in Phase 8 scope |
| `Serilog` | existing config | Structured install logs | Use `_logger.LogInformation("Downloaded {Tag} ({Size} bytes)", tag, size)` — never string interpolation in log calls (per CLAUDE.md) |
| `xunit` | 2.9.2 | Unit tests | Pattern established in `tests/WindroseServerManager.Core.Tests/` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ZipFile.ExtractToDirectory` | `ZipArchive` with per-entry `CopyToAsync` + cancellation | Needed only if we want mid-extract cancellation. For a 5 MB archive, atomic temp-extract is fast enough that per-entry CT support is overkill. |
| `File.Move` | P/Invoke `ReplaceFileW` via `LibraryImport` | `ReplaceFile` is more robust when the destination exists and is locked, but `File.Move(source, dest, overwrite: true)` on same-volume NTFS is already atomic. We do not need `ReplaceFile` if we rely on directory-level replacement of vendor folders. |
| `IHttpClientFactory` named client | Static `HttpClient` | Named client gets DI-managed lifetime + existing Polly/retry if added later. Mirror `AppUpdateService`. |

**Installation:** No new NuGet packages required. All dependencies are already present in the repo.

**Version verification:** Not applicable (BCL only). The WindrosePlus upstream version we pin-test against is **v1.0.6** (published 2026-04-19).

## Architecture Patterns

### Recommended Project Structure

```
src/WindroseServerManager.Core/
├── Services/
│   ├── IWindrosePlusService.cs          # New: install contract + launcher resolution
│   ├── WindrosePlusService.cs           # New: implementation, mirrors SteamCmdService
│   └── ServerProcessService.cs          # Modified: StartAsync queries IWindrosePlusService
├── Models/
│   ├── WindrosePlusRelease.cs           # New: record for API response (tag, assetUrl, digest, size)
│   ├── WindrosePlusInstallResult.cs     # New: record (installed tag, path, timestamp, bundledLicensePath)
│   ├── InstallProgress.cs               # Modified: add Phase values (FetchingRelease, Downloading, Verifying, Extracting, Installing)
│   └── ServerInstallInfo.cs             # Modified: add WindrosePlusActive + WindrosePlusVersionTag

src/WindroseServerManager.App/
├── Services/
│   └── (reuse existing ToastService / ErrorMessageHelper — no new services)
├── Views/
│   └── Dialogs/
│       └── AboutDialog.xaml             # Modified: new "Third-Party Licenses" section
└── Resources/
    ├── Licenses/
    │   └── WindrosePlus-LICENSE.txt     # New: embedded resource, shipped in install output
    └── Strings/
        ├── Strings.de.xaml              # New keys: About.ThirdPartyLicenses, Error.OfflineInstall, Warning.MissingBat
        └── Strings.en.xaml              # Same keys in English

tests/WindroseServerManager.Core.Tests/
└── WindrosePlusServiceTests.cs          # New: SHA verification, atomic install, launcher resolution
```

### Pattern 1: Mirror `SteamCmdService` Shape

**What:** `WindrosePlusService` reuses the exact pattern that's already proven in this codebase — `SemaphoreSlim` for concurrency guard, `IProgress<string>` for log lines, `CancellationToken` threaded through every async call.

**When to use:** Any long-running install operation in this codebase.

**Example:**
```csharp
// Source: src/WindroseServerManager.Core/Services/SteamCmdService.cs (existing pattern)
public sealed class WindrosePlusService : IWindrosePlusService
{
    private const string ApiUrl = "https://api.github.com/repos/HumanGenome/WindrosePlus/releases/latest";
    private const string UserAgent = "WindroseServerManager-WindrosePlus";
    private readonly ILogger<WindrosePlusService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _cacheDir;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public WindrosePlusService(ILogger<WindrosePlusService> logger, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindroseServerManager", "cache", "windroseplus");
    }

    public async Task<WindrosePlusInstallResult> InstallAsync(
        string serverInstallDir,
        IProgress<InstallProgress>? progress,
        CancellationToken ct = default)
    {
        await _installLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            progress?.Report(new InstallProgress(InstallPhase.Preparing, "Lade WindrosePlus-Release-Info...", null, null));
            var release = await FetchLatestAsync(ct).ConfigureAwait(false);

            progress?.Report(new InstallProgress(InstallPhase.Preparing, "Prüfe Cache...", null, null));
            var archivePath = await EnsureArchiveCachedAsync(release, progress, ct).ConfigureAwait(false);

            progress?.Report(new InstallProgress(InstallPhase.Validating, "Verifiziere SHA256...", null, null));
            await VerifyDigestAsync(archivePath, release.DigestSha256, ct).ConfigureAwait(false);

            var tempExtractDir = Path.Combine(_cacheDir, $"_extract_{Guid.NewGuid():N}");
            try
            {
                ZipFile.ExtractToDirectory(archivePath, tempExtractDir);
                AtomicCopyIntoServer(tempExtractDir, serverInstallDir, preserveUserConfig: true);
                WriteVersionMarker(serverInstallDir, release.Tag);
                CopyLicense(tempExtractDir, serverInstallDir);
                return new WindrosePlusInstallResult(release.Tag, serverInstallDir, DateTime.UtcNow);
            }
            finally
            {
                try { Directory.Delete(tempExtractDir, recursive: true); }
                catch (Exception ex) { _logger.LogDebug(ex, "Temp extract cleanup failed"); }
            }
        }
        finally { _installLock.Release(); }
    }
}
```

### Pattern 2: GitHub API Call — Reuse `AppUpdateService` Shape

**What:** Identical HttpClient setup — `UserAgent` + `Accept: application/vnd.github+json` + 10-second timeout.

**Why:** Consistency; future rate-limit handling or Bearer-token injection can be added in one place.

**Example:**
```csharp
// Source: src/WindroseServerManager.App/Services/AppUpdateService.cs:41-47 (mirror exactly)
var http = _httpFactory.CreateClient();
http.Timeout = TimeSpan.FromSeconds(10);
http.DefaultRequestHeaders.UserAgent.Clear();
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, currentAppVersion));
http.DefaultRequestHeaders.Accept.Clear();
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

using var resp = await http.GetAsync(ApiUrl, ct).ConfigureAwait(false);
resp.EnsureSuccessStatusCode();
await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
// doc.RootElement has: tag_name, assets[].name, assets[].browser_download_url, assets[].digest, assets[].size
```

### Pattern 3: Launcher Resolution Indirection

**What:** `ServerProcessService.StartAsync` currently hardcodes `ServerInstallService.FindServerBinary(dir)` (see `ServerProcessService.cs:44-46`). Add an injection point.

**Example:**
```csharp
// In IWindrosePlusService:
(string exePath, string extraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info);

// In ServerProcessService.StartAsync (replacing current exe-resolution):
var (exe, wPlusArgs) = _windrosePlus.ResolveLauncher(dir, _settings.Current.CurrentServerInfo);
var args = BuildLaunchArgs(_settings.Current) + (string.IsNullOrWhiteSpace(wPlusArgs) ? "" : " " + wPlusArgs);
// Missing-.bat fallback lives inside ResolveLauncher:
//   if (info.WindrosePlusActive && !File.Exists(batPath)) { _toast.Warning(...); return (WindroseServer.exe, ""); }
```

### Anti-Patterns to Avoid

- **Direct `HttpClient` instantiation:** Violates the project's `IHttpClientFactory` convention. Every existing service uses the factory — don't break the pattern.
- **`Path.Combine` without `Path.GetFullPath`:** `ServerInstallService.ValidateInstallDir` already normalizes; reuse that validation before writing anywhere.
- **String-interpolation in log calls:** Per global coding-standards: `Log.Information("{Tag}", tag)` not `Log.Information($"... {tag} ...")`. Enforce in every log call.
- **Sync-over-async on install:** `async void` Task fire-and-forget without try-catch is banned per global rules. All fire-and-forget must be `_ = Task.Run(async () => { try { ... } catch (Exception ex) { _logger.LogError(ex, ...); } });`
- **Silent overwrite of user config:** The install copy step MUST skip `windrose_plus.json`, `windrose_plus.ini`, and the entire `WindrosePlus/Mods/` directory if present (see §"Config-File Preservation").

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Download with progress | Manual `Stream.ReadAsync` loop counting bytes | `HttpCompletionOption.ResponseHeadersRead` + `Content.CopyToAsync(fs, ct)` (as `SteamCmdService.cs:50-54` does). For progress, wrap output stream in a counting stream. | Framework handles partial reads, chunked encoding, cancellation correctly |
| Zip extraction | Custom entry-by-entry copy | `ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true)` | Handles zip slip, encoding, directory creation in one call — already used by `SteamCmdService` |
| SHA-256 verification | Manual `Stream` chunking + `HashAlgorithm.TransformBlock` | `await SHA256.HashDataAsync(stream, ct)` (.NET 5+) | One line, streaming, cancellable |
| Atomic file replacement on NTFS | Custom P/Invoke `ReplaceFileW` | `File.Move(source, dest, overwrite: true)` for files; directory-level: move-out-old → move-in-new | Same-volume NTFS move is atomic at the file level; for directories the "swap via temp" pattern is standard |
| GitHub Releases JSON parsing | Custom regex | `JsonDocument.ParseAsync` + `TryGetProperty` (exactly like `AppUpdateService.cs:57-82`) | Battle-tested shape in this repo |
| Local cache eviction | LRU with size tracking | Keep only `latest.zip` + `latest.json` (metadata); overwrite on each fetch | Cache is fallback-only, not a growable artifact store |

**Key insight:** Every sub-problem in this phase already has a production-tested solution in the same codebase (`SteamCmdService`, `AppUpdateService`). The risk is not "can we build it" but "do we stay consistent with the existing patterns." Deviation adds cognitive load for every future reader.

## Common Pitfalls

### Pitfall 1: UE4SS not in the release archive
**What goes wrong:** The locked decision says "UE4SS comes entirely from the WindrosePlus GitHub release asset." Inspection of the live v1.0.6 release shows `WindrosePlus.zip` contains `install.ps1` which downloads UE4SS from `https://github.com/UE4SS-RE/RE-UE4SS/releases/...` at install time. If our `WindrosePlusService` just extracts the zip, we do NOT get a working UE4SS install — the user would need to run install.ps1 separately (which we explicitly don't want).
**Why it happens:** HumanGenome optimizes release size by not rebundling UE4SS.
**How to avoid:** Three options for the planner:
1. **Replicate install.ps1 in C#:** Also fetch the UE4SS experimental release from GitHub (2nd API call) and layer it into our atomic install. Means maintaining an asset-name pattern ("UE4SS_*.zip") but decouples us from the PowerShell script.
2. **Shell out to install.ps1:** Run the bundled script via `powershell.exe -ExecutionPolicy Bypass -File install.ps1` inside the extracted temp dir. Fragile — bypasses our progress reporting, breaks cache semantics.
3. **Coordinate upstream:** File an issue asking HumanGenome to publish a "full" release asset with UE4SS inlined. User has already opened a collaboration issue (per STATE.md) — could be part of that.
**Warning signs:** Zip size < 6 MB (UE4SS alone is ~20 MB); no `dwmapi.dll` or `ue4ss/` folder present after extract. Add an assertion step: after `AtomicCopyIntoServer`, verify `R5\Binaries\Win64\dwmapi.dll` exists or throw.

### Pitfall 2: SHA verification fails on old releases
**What goes wrong:** GitHub asset digests (the `digest` field in `/releases/latest`) were rolled out in June 2025. Assets uploaded before that date return `null`. The current v1.0.6 has a digest (verified), but a future rollback to an older tag wouldn't.
**Why it happens:** Retroactive digest computation is opt-in.
**How to avoid:** Treat `digest` as optional — if missing, either skip verification with a warning log (acceptable since we still cache the exact bytes served) or compute our own SHA and persist it in our cache metadata. Do not hard-fail on missing upstream digest.
**Warning signs:** `TryGetProperty("digest", out _)` returns `false` or the property is `null`.

### Pitfall 3: Cross-volume move breaks atomicity
**What goes wrong:** User's `%LocalAppData%` is on C:, server install is on D:. `File.Move` silently degrades to copy+delete across volumes, which is NOT atomic — a mid-operation crash leaves partial state in the destination.
**Why it happens:** Windows filesystem semantics; `MOVEFILE_COPY_ALLOWED` is implicit in `File.Move`.
**How to avoid:** Extract to a temp dir **on the same volume as the server install** (e.g., `{serverDir}\..\.wplus-install-temp-{guid}\`), not to `%LocalAppData%`. After verification, move-rename within that volume. Only the downloaded ZIP stays in `%LocalAppData%`.
**Warning signs:** Server dir drive letter ≠ `%LocalAppData%` drive letter. Detect with `Path.GetPathRoot(serverDir) != Path.GetPathRoot(_cacheDir)`.

### Pitfall 4: Launched .bat inherits wrong working directory
**What goes wrong:** `ServerProcessService` sets `WorkingDirectory = Path.GetDirectoryName(exe)!`. If `exe` is `StartWindrosePlusServer.bat` at server root, `Path.GetDirectoryName` returns the server root — correct. But if the .bat uses relative paths that assume CWD, and we ever invoke from a different path, things break.
**Why it happens:** Batch files are path-sensitive.
**How to avoid:** Always set `WorkingDirectory` to the .bat's parent directory explicitly. This is already the pattern in `ServerProcessService.cs:74`; just ensure the exe-resolution returns the .bat's full path, not a relative one.
**Warning signs:** Server log shows "file not found" errors for `ue4ss\...` or `windrose_plus\...` — sign CWD is wrong.

### Pitfall 5: Concurrent install attempts corrupt extraction
**What goes wrong:** User clicks "install" twice (double-click on a slow first-launch). Two extractions race into the same directory.
**Why it happens:** No guard on the public method.
**How to avoid:** Use the same `SemaphoreSlim(1,1)` pattern as `SteamCmdService._ensureLock`. Per-instance is sufficient because `IWindrosePlusService` is Singleton.
**Warning signs:** `IOException: file in use` during extract.

### Pitfall 6: GitHub rate limit (60 req/h unauth)
**What goes wrong:** A user on a shared NAT with aggressive retry could hit the 60/h unauthenticated ceiling.
**Why it happens:** Shared IP.
**How to avoid:** Cache the `/releases/latest` JSON response itself for 10 minutes (`latest.json` next to `latest.zip`). Any install within 10 minutes of last check skips the API round-trip. Acceptable staleness for a user-initiated operation.
**Warning signs:** `HTTP 403` with `X-RateLimit-Remaining: 0` header.

## Code Examples

### Fetching the latest release + picking the right asset
```csharp
// Source: pattern from src/WindroseServerManager.App/Services/AppUpdateService.cs:41-82
// Adapted for: WindrosePlus asset selection (WindrosePlus.zip, not setup.exe)
private async Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct)
{
    var http = _httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(10);
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, _appVersion));
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

    using var resp = await http.GetAsync(ApiUrl, ct).ConfigureAwait(false);
    resp.EnsureSuccessStatusCode();
    await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    var root = doc.RootElement;

    if (root.TryGetProperty("draft", out var d) && d.GetBoolean()) throw new InvalidOperationException("Latest release is draft");
    if (root.TryGetProperty("prerelease", out var p) && p.GetBoolean()) throw new InvalidOperationException("Latest release is prerelease");

    var tag = root.GetProperty("tag_name").GetString() ?? throw new InvalidOperationException("Missing tag_name");
    var assets = root.GetProperty("assets");

    foreach (var asset in assets.EnumerateArray())
    {
        var name = asset.GetProperty("name").GetString();
        if (!string.Equals(name, "WindrosePlus.zip", StringComparison.OrdinalIgnoreCase)) continue;

        var url = asset.GetProperty("browser_download_url").GetString()!;
        var size = asset.GetProperty("size").GetInt64();
        string? digest = asset.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String
            ? dg.GetString() : null; // e.g. "sha256:110ad424..."
        return new WindrosePlusRelease(tag, url, size, digest);
    }
    throw new InvalidOperationException("WindrosePlus.zip asset not found in latest release");
}
```

### SHA-256 verification (streaming, cancellable)
```csharp
// Source: .NET docs — SHA256.HashDataAsync (added in .NET 5)
private static async Task VerifyDigestAsync(string path, string? expectedDigest, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(expectedDigest))
    {
        // Pre-June-2025 releases have no digest; skip verification with log warning.
        return;
    }
    var prefix = "sha256:";
    var expectedHex = expectedDigest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? expectedDigest[prefix.Length..] : expectedDigest;

    await using var fs = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
    var actualHex = Convert.ToHexString(hash);
    if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"WindrosePlus archive SHA256 mismatch. Expected {expectedHex}, got {actualHex}");
    }
}
```

### Atomic copy preserving user config
```csharp
// Source: combined pattern — SteamCmdService extraction + Phase 8 config-preservation rules
private static readonly string[] UserOwnedRelativePaths =
{
    "windrose_plus.json",                       // multipliers, RCON password, admin Steam IDs
    "windrose_plus.ini",                        // advanced tuning (user-copy from .default.ini)
    "windrose_plus\\config\\windrose_plus.ini", // depends on HumanGenome layout — verify on first real install
    "R5\\Binaries\\Win64\\ue4ss\\Mods\\WindrosePlus\\Scripts\\user_overrides.lua", // if exists
};

private static readonly string[] UserOwnedDirectories =
{
    "R5\\Binaries\\Win64\\ue4ss\\Mods\\",  // user-installed lua mods (everything under Mods/ except vendor folder WindrosePlus/)
};

private void AtomicCopyIntoServer(string tempExtractRoot, string serverDir, bool preserveUserConfig)
{
    foreach (var sourceFile in Directory.EnumerateFiles(tempExtractRoot, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(tempExtractRoot, sourceFile);
        var dest = Path.Combine(serverDir, rel);

        if (preserveUserConfig && UserOwnedRelativePaths.Any(u => rel.Equals(u, StringComparison.OrdinalIgnoreCase)))
        {
            if (File.Exists(dest))
            {
                _logger.LogInformation("Preserving user config: {Rel}", rel);
                continue;
            }
            // else: user config doesn't exist yet — fall through to copy the template
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        // File.Move(src, dest, overwrite:true) is atomic on same-volume NTFS.
        // Destination must not be locked; ServerProcessService must be Stopped before install.
        File.Move(sourceFile, dest, overwrite: true);
    }
}
```

### Launcher resolution with missing-.bat fallback
```csharp
// Source: new — implements WPLUS-04 per CONTEXT.md decisions
public (string exePath, string extraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info)
{
    if (!info.WindrosePlusActive)
    {
        var exe = ServerInstallService.FindServerBinary(serverInstallDir)
                  ?? throw new FileNotFoundException("WindroseServer.exe not found", serverInstallDir);
        return (exe, string.Empty);
    }

    var bat = Path.Combine(serverInstallDir, "StartWindrosePlusServer.bat");
    if (File.Exists(bat))
    {
        return (bat, string.Empty);
    }

    // Flag says active but .bat missing — soft fallback per CONTEXT.md
    _logger.LogWarning("WindrosePlus flagged active but StartWindrosePlusServer.bat missing at {Path} — falling back to WindroseServer.exe", bat);
    _toast.Warning(Localize("Warning.WindrosePlusBatMissing"));
    var fallback = ServerInstallService.FindServerBinary(serverInstallDir)
                   ?? throw new FileNotFoundException("Neither .bat nor .exe found", serverInstallDir);
    return (fallback, string.Empty);
}
```

### Upstream Archive Layout (v1.0.6, verified 2026-04-19)

Based on README + install.ps1 fetch (see §"Sources"):

```
WindrosePlus.zip (5.3 MB)
├── install.ps1                    # PowerShell installer — DOWNLOADS UE4SS at runtime
├── StartWindrosePlusServer.bat    # launcher our service must run
├── UE4SS-settings.ini             # UE4SS proxy config
├── LICENSE                        # MIT (HumanGenome) — bundle this
├── README.md
├── WindrosePlus/                  # main mod framework
├── config/                        # *.default.ini templates
│   └── windrose_plus.default.ini
├── cpp-mods/                      # C++ extension modules (compiled DLLs)
├── docs/
├── server/                        # dashboard server
├── tools/
└── .github/                       # ignore

# Files NOT in the zip (install.ps1 downloads them):
# - UE4SS proxy DLL (typically dwmapi.dll) → R5\Binaries\Win64\
# - UE4SS runtime → R5\Binaries\Win64\ue4ss\

# Files created on first server start (MUST be preserved on reinstall):
# - windrose_plus.json  (at server root, per README)
# - windrose_plus.ini   (copied from config/windrose_plus.default.ini, user-modified)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Publish SHA256SUMS.txt as separate asset | `assets[].digest` field in Releases API | GitHub — June 2025 | No separate file to fetch; one API call has both URL and digest |
| `File.Move(src, dest)` (no overwrite overload) | `File.Move(src, dest, overwrite: true)` | .NET 5 | Removed need for `File.Delete(dest); File.Move(src, dest);` pre-step |
| `HashAlgorithm.ComputeHash(stream)` | `SHA256.HashDataAsync(stream, ct)` | .NET 5 | One line, cancellable, no disposable HashAlgorithm |
| Manually rehydrating HttpClient | `IHttpClientFactory` via DI | .NET Core 2.1 | Already adopted project-wide |

**Deprecated/outdated:**
- None relevant to this phase. All Context7/BCL APIs used are current for .NET 9.

## Open Questions

1. **How do we handle the fact that UE4SS is NOT in the upstream zip?** (CRITICAL — blocks WPLUS-02)
   - What we know: `WindrosePlus.zip` v1.0.6 is 5.3 MB and contains `install.ps1` which fetches UE4SS from `UE4SS-RE/RE-UE4SS` at install time. The locked CONTEXT decision assumes UE4SS is bundled — it isn't.
   - What's unclear: Whether HumanGenome plans to inline UE4SS in a future release, or whether we should replicate install.ps1's fetch logic in C#.
   - Recommendation: Surface this in the first planning call. Most pragmatic path: have `WindrosePlusService` also call the UE4SS GitHub API as a second step of `InstallAsync` (reuse the same HttpClient pattern), cache both archives under `%LocalAppData%\...\cache\windroseplus\` and `%LocalAppData%\...\cache\ue4ss\`, verify both, extract both into the atomic temp, move both into place. Adds ~15-20 MB to cache and a second API call. Alternative: call the bundled `install.ps1` via `powershell.exe` — rejected because it bypasses our progress/cancellation surface.

2. **Exact user-config preservation list** — confirm against a real HumanGenome install.
   - What we know: README mentions `windrose_plus.json` (runtime-created) and `windrose_plus.ini` (copied from `.default.ini`) as user-owned.
   - What's unclear: Whether `Mods/` under `ue4ss` has any vendor subfolder besides `WindrosePlus/` that we must overwrite, or whether the whole `Mods/` tree is user-owned.
   - Recommendation: Plan a "verify install output" smoke test early in Phase 8 (install once in a throwaway dir, `tree /F` the result, compare to the list above). Tune the `UserOwnedRelativePaths` / `UserOwnedDirectories` arrays before shipping.

3. **Should .wplus-version be JSON or plaintext?**
   - What we know: CONTEXT says "`.wplus-version` file (or equivalent)" — discretionary.
   - Recommendation: JSON `{ "tag": "v1.0.6", "installedUtc": "2026-04-19T10:00:00Z", "archiveSha256": "110ad..." }`. Lets Phase 10 health check verify the install wasn't tampered with post-install and Phase 11 UPGRADE-01 compare versions without reparsing the zip.

4. **Where exactly does `ServerInstallInfo.WindrosePlusActive` get persisted?**
   - What we know: `ServerInstallInfo` is currently an immutable record; `AppSettings` is the mutable persisted store (per `AppSettings.cs`).
   - What's unclear: v1.0/v1.1 stores install metadata at which layer — per-server in `AppSettings` or on-disk in the server dir?
   - Recommendation: Planner should read `AppSettingsService.cs` and `ServerInstallService.cs` Detect path early. The flag likely lives in `AppSettings` keyed by server ID since `ServerInstallInfo` is a read-through record computed from disk state.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 (+ Microsoft.NET.Test.Sdk 17.11.1) |
| Config file | `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` |
| Quick run command | `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~WindrosePlus" --nologo -v minimal` |
| Full suite command | `dotnet test tests/WindroseServerManager.Core.Tests --nologo` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| WPLUS-01 | Fetch latest release, parse tag + asset URL + digest | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.FetchLatest_ParsesTagAndDigest` | ❌ Wave 0 |
| WPLUS-01 | Cache fallback when offline (HttpClient throws) | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_UsesCache_WhenApiUnreachable_AndCacheExists` | ❌ Wave 0 |
| WPLUS-01 | Offline + empty cache → hard fail with typed exception | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_ThrowsOfflineInstallException_WhenNoCache` | ❌ Wave 0 |
| WPLUS-02 | Atomic install — mid-extract crash leaves no broken server | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_IsAtomic_TempDirFailure_DoesNotTouchServerDir` | ❌ Wave 0 |
| WPLUS-02 | SHA256 verification rejects tampered archive | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_ThrowsShaMismatch_WhenArchiveModified` | ❌ Wave 0 |
| WPLUS-02 | User config (`windrose_plus.json`) preserved across reinstall | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_PreservesExistingUserConfig` | ❌ Wave 0 |
| WPLUS-02 | Vendor files (e.g. StartWindrosePlusServer.bat) overwritten | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_OverwritesVendorBinaries` | ❌ Wave 0 |
| WPLUS-02 | `.wplus-version` marker written with correct tag | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_WritesVersionMarker` | ❌ Wave 0 |
| WPLUS-03 | LICENSE copied into install output | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.Install_CopiesLicenseToServerDir` | ❌ Wave 0 |
| WPLUS-03 | About dialog renders MIT text + HumanGenome link | manual-only | `Manual: open app → About → Third-Party Licenses → verify MIT text + link` | n/a — UI smoke check on integration build |
| WPLUS-04 | Active flag + .bat present → launches StartWindrosePlusServer.bat | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.ResolveLauncher_Active_ReturnsBat` | ❌ Wave 0 |
| WPLUS-04 | Opted out → launches WindroseServer.exe | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.ResolveLauncher_OptedOut_ReturnsExe` | ❌ Wave 0 |
| WPLUS-04 | Active flag + .bat missing → falls back to exe + warning | unit | `dotnet test --filter FullyQualifiedName~WindrosePlusServiceTests.ResolveLauncher_Active_BatMissing_FallsBackWithWarning` | ❌ Wave 0 |
| WPLUS-04 | `ServerInstallInfo.WindrosePlusActive` round-trips through AppSettings persist/load | unit | `dotnet test --filter FullyQualifiedName~AppSettingsTests.WindrosePlusActive_RoundTrip` | ❌ Wave 0 (extend existing AppSettingsTests.cs) |

**Manual-only justification (WPLUS-03 About UI):** Rendering of a static MIT text block and a hyperlink in an Avalonia FluentWindow is trivial and visually obvious — automated UI testing would add Avalonia.Headless overhead disproportionate to the value. Manual smoke is adequate and matches existing v1.0/v1.1 QA rhythm for About-dialog changes.

### Sampling Rate
- **Per task commit:** `dotnet test tests/WindroseServerManager.Core.Tests --filter "FullyQualifiedName~WindrosePlus" --nologo -v minimal` (target: < 10 s)
- **Per wave merge:** `dotnet test tests/WindroseServerManager.Core.Tests --nologo` (full suite, ~30 s)
- **Phase gate:** Full suite green + manual About-dialog smoke before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/WindroseServerManager.Core.Tests/WindrosePlusServiceTests.cs` — covers WPLUS-01, WPLUS-02, WPLUS-03, WPLUS-04
- [ ] Extend `tests/WindroseServerManager.Core.Tests/AppSettingsTests.cs` with `WindrosePlusActive_RoundTrip` test once the property is added to `ServerInstallInfo`/`AppSettings`
- [ ] Test fixture directory: `tests/WindroseServerManager.Core.Tests/Fixtures/windroseplus/` containing (a) a minimal valid mock WindrosePlus.zip built by a test helper (5-10 files + LICENSE + StartWindrosePlusServer.bat + windrose_plus.json) and (b) a tampered copy with wrong SHA. Prefer building zip in-test with `System.IO.Compression.ZipArchive` over checking binaries into the repo.
- [ ] Test double for `IHttpClientFactory` — existing tests don't mock it yet. Add a simple `FakeHttpMessageHandler` (records requests, returns scripted responses) under `tests/.../TestDoubles/`.

## Sources

### Primary (HIGH confidence)
- `src/WindroseServerManager.Core/Services/SteamCmdService.cs` — pattern for async installer + IProgress + cancellation
- `src/WindroseServerManager.App/Services/AppUpdateService.cs` — pattern for GitHub Releases API call in this codebase
- `src/WindroseServerManager.Core/Services/ServerProcessService.cs` — launcher integration point (lines 44-82)
- `src/WindroseServerManager.Core/Models/InstallProgress.cs` — existing progress DTO to extend
- `src/WindroseServerManager.Core/Models/ServerInstallInfo.cs` — existing per-server record to extend
- `tests/WindroseServerManager.Core.Tests/WindroseServerManager.Core.Tests.csproj` — xunit 2.9.2 stack
- `.planning/phases/08-windroseplus-bootstrap/08-CONTEXT.md` — user-locked decisions
- GitHub API `GET /repos/HumanGenome/WindrosePlus/releases/latest` — verified v1.0.6 response with `WindrosePlus.zip` + `digest: sha256:110ad424...` (fetched 2026-04-19)
- [GitHub Changelog — Releases now expose digests for release assets (June 2025)](https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/)

### Secondary (MEDIUM confidence)
- [HumanGenome/WindrosePlus README](https://github.com/HumanGenome/WindrosePlus) — archive layout, install steps, launcher filename
- [HumanGenome/WindrosePlus/install.ps1](https://github.com/HumanGenome/WindrosePlus/blob/main/install.ps1) — UE4SS is fetched at install-time, NOT bundled
- [GitHub REST API — release assets](https://docs.github.com/en/rest/releases/assets) — `digest` field spec
- [Antony Male — Atomic File Writes on Windows](https://antonymale.co.uk/windows-atomic-file-writes.html) — ReplaceFile vs MoveFileEx semantics
- [MoveFileEx(MOVEFILE_REPLACE_EXISTING) + NTFS — Microsoft Learn](https://learn.microsoft.com/en-us/archive/msdn-technet-forums/449bb49d-8acc-48dc-a46f-0760ceddbfc3) — same-volume atomicity

### Tertiary (LOW confidence — needs real-install verification)
- Exact list of user-owned files inside WindrosePlus install output — derived from README quotes and install.ps1 analysis but not verified by running a real install. Recommend a Wave 0 smoke-install task to tune `UserOwnedRelativePaths`.

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — every API used is already in the codebase; no new deps.
- Architecture: **HIGH** — mirrors two existing, production-tested services (`SteamCmdService`, `AppUpdateService`).
- Pitfalls: **HIGH** for cross-volume / concurrency / config-preservation (well-known Windows + .NET semantics). **MEDIUM** for pitfall #1 (UE4SS not bundled) — verified via README + install.ps1 fetch but would be HIGH if we'd downloaded and `tree /F`-inspected the actual v1.0.6 zip.
- Validation Architecture: **HIGH** — xunit stack already wired; test doubles are a trivial addition.

**Research date:** 2026-04-19
**Valid until:** 2026-05-19 (30 days) for the patterns; **re-check WindrosePlus release contents on any new upstream tag** — the archive layout is the biggest stability risk.
