using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class ModService : IModService
{
    private const string ModsRelativeDir = @"R5\Content\Paks\~mods";
    private const string DisabledSuffix = ".disabled";
    private const string MetaSuffix = ".meta.json";
    private static readonly string[] PakExtensions = [".pak", ".ucas", ".utoc"];

    private static readonly JsonSerializerOptions MetaJsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<ModService> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerProcessService _process;

    public ModService(
        ILogger<ModService> logger,
        IAppSettingsService settings,
        IServerProcessService process)
    {
        _logger = logger;
        _settings = settings;
        _process = process;
    }

    public string? GetModsDir()
    {
        var install = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(install)) return null;
        var dir = Path.Combine(install, ModsRelativeDir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string? ValidateReady()
    {
        if (string.IsNullOrWhiteSpace(_settings.ActiveServerDir))
            return "Server install path is not set. Set up the installation first.";

        if (_process.Status is ServerStatus.Running or ServerStatus.Starting)
            return "Server is running. Mod changes require a stopped server.";

        return null;
    }

    public IEnumerable<ModInfo> ListMods()
    {
        var dir = GetModsDir();
        if (dir is null || !Directory.Exists(dir)) yield break;

        // Primär-Einträge: alles das auf .pak oder .pak.disabled endet
        var allFiles = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        foreach (var path in allFiles)
        {
            var name = Path.GetFileName(path);
            var enabled = IsEnabledPak(name);
            var disabled = IsDisabledPak(name);
            if (!enabled && !disabled) continue;

            var baseName = enabled
                ? Path.GetFileNameWithoutExtension(name)
                : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(name));

            var companions = FindCompanions(dir, baseName);
            var fi = new FileInfo(path);
            var display = BuildDisplayName(baseName);
            var meta = ReadMetaFile(Path.Combine(dir, name + MetaSuffix));

            yield return new ModInfo(
                FileName: name,
                FullPath: fi.FullName,
                DisplayName: display,
                SizeBytes: fi.Length + companions.Sum(c => new FileInfo(Path.Combine(dir, c)).Length),
                InstalledUtc: fi.CreationTimeUtc,
                IsEnabled: enabled,
                CompanionFiles: companions,
                NexusMeta: meta);
        }
    }

    public ModMeta? GetMeta(string fileName)
    {
        var dir = GetModsDir();
        if (dir is null) return null;
        return ReadMetaFile(Path.Combine(dir, fileName + MetaSuffix));
    }

    public void SetMeta(string fileName, ModMeta meta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(meta);
        var dir = GetModsDir() ?? throw new InvalidOperationException("Mods-Verzeichnis nicht ermittelbar.");

        var primary = Path.Combine(dir, fileName);
        if (!File.Exists(primary))
            throw new FileNotFoundException("Primary mod file does not exist.", primary);

        var metaPath = primary + MetaSuffix;
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, MetaJsonOpts));
        _logger.LogInformation("Nexus-Verknüpfung für {File} gespeichert (Mod #{ModId}).",
            fileName, meta.NexusModId);
    }

    public void ClearMeta(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var dir = GetModsDir();
        if (dir is null) return;

        var metaPath = Path.Combine(dir, fileName + MetaSuffix);
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
            _logger.LogInformation("Nexus-Verknüpfung für {File} entfernt.", fileName);
        }
    }

    private static ModMeta? ReadMetaFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModMeta>(json, MetaJsonOpts);
        }
        catch
        {
            // Korrupte Side-Car-Datei — einfach ignorieren, der User kann sie neu verlinken.
            return null;
        }
    }

    public async Task<IReadOnlyList<ModInfo>> InstallFromArchiveAsync(string sourcePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Quelldatei nicht gefunden", sourcePath);

        var ready = ValidateReady();
        if (ready is not null) throw new InvalidOperationException(ready);

        var modsDir = GetModsDir()
            ?? throw new InvalidOperationException("Mods-Verzeichnis nicht ermittelbar.");

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        List<string> extractedPaks;

        if (ext == ".pak")
        {
            extractedPaks = await Task.Run(() => CopyPakAndCompanions(sourcePath, modsDir), ct)
                .ConfigureAwait(false);
        }
        else if (ext == ".zip")
        {
            extractedPaks = await Task.Run(() => ExtractZip(sourcePath, modsDir, ct), ct)
                .ConfigureAwait(false);
        }
        else if (ext == ".7z")
        {
            extractedPaks = await Task.Run(() => Extract7z(sourcePath, modsDir, ct), ct)
                .ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported format: {ext}. Only .pak, .zip, .7z supported.");
        }

        if (extractedPaks.Count == 0)
            throw new InvalidOperationException(
                "Keine .pak-Datei im Archiv gefunden. Details im Log (%LOCALAPPDATA%\\WindroseServerManager\\logs).");

        _logger.LogInformation("Installiert {Count} Mod(s) aus {Source}", extractedPaks.Count, sourcePath);

        var all = ListMods().ToList();
        var result = extractedPaks
            .Select(name => all.FirstOrDefault(m =>
                string.Equals(m.FileName, name, StringComparison.OrdinalIgnoreCase)))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();
        if (result.Count == 0)
            throw new InvalidOperationException("Installierter Mod konnte nicht gelesen werden.");
        return result;
    }

    public void SetEnabled(string fileName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var dir = GetModsDir() ?? throw new InvalidOperationException("Mods-Verzeichnis nicht ermittelbar.");

        var source = Path.Combine(dir, fileName);
        if (!File.Exists(source))
        {
            _logger.LogDebug("SetEnabled für fehlende Datei: {File}", fileName);
            return;
        }

        var currentlyEnabled = IsEnabledPak(fileName);
        if (currentlyEnabled == enabled) return; // Idempotent

        var baseName = currentlyEnabled
            ? Path.GetFileNameWithoutExtension(fileName)
            : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName));

        var newName = enabled ? $"{baseName}.pak" : $"{baseName}.pak{DisabledSuffix}";
        var target = Path.Combine(dir, newName);

        File.Move(source, target, overwrite: false);

        // Side-Car-Meta mitziehen, damit Nexus-Verknüpfung beim Enable/Disable erhalten bleibt.
        var oldMeta = source + MetaSuffix;
        var newMeta = target + MetaSuffix;
        if (File.Exists(oldMeta)) File.Move(oldMeta, newMeta, overwrite: true);

        _logger.LogInformation("Mod {Old} → {New}", fileName, newName);
    }

    public void UninstallMod(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var dir = GetModsDir() ?? throw new InvalidOperationException("Mods-Verzeichnis nicht ermittelbar.");

        var primary = Path.Combine(dir, fileName);
        if (!File.Exists(primary))
        {
            _logger.LogDebug("Uninstall für fehlende Datei: {File}", fileName);
            return;
        }

        var baseName = IsEnabledPak(fileName)
            ? Path.GetFileNameWithoutExtension(fileName)
            : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName));

        foreach (var companion in FindCompanions(dir, baseName))
        {
            var cp = Path.Combine(dir, companion);
            try { File.Delete(cp); }
            catch (Exception ex) { _logger.LogWarning(ex, "Companion konnte nicht gelöscht werden: {File}", companion); }
        }

        // Side-Car-Meta ebenfalls löschen
        var metaPath = primary + MetaSuffix;
        if (File.Exists(metaPath))
        {
            try { File.Delete(metaPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Side-Car-Meta konnte nicht gelöscht werden: {File}", metaPath); }
        }

        File.Delete(primary);
        _logger.LogInformation("Mod {File} deinstalliert (inkl. Companions)", fileName);
    }

    public async Task<string> ExportClientBundleAsync(string targetZipPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetZipPath);
        var dir = GetModsDir() ?? throw new InvalidOperationException("Mods-Verzeichnis nicht ermittelbar.");

        var activeMods = ListMods().Where(m => m.IsEnabled).ToList();
        if (activeMods.Count == 0)
            throw new InvalidOperationException("Keine aktiven Mods zum Exportieren.");

        await Task.Run(() =>
        {
            if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
            using var zip = ZipFile.Open(targetZipPath, ZipArchiveMode.Create);
            foreach (var mod in activeMods)
            {
                ct.ThrowIfCancellationRequested();
                zip.CreateEntryFromFile(mod.FullPath, mod.FileName, CompressionLevel.Fastest);
                foreach (var companion in mod.CompanionFiles)
                {
                    var cpath = Path.Combine(dir, companion);
                    if (File.Exists(cpath))
                        zip.CreateEntryFromFile(cpath, companion, CompressionLevel.Fastest);
                }
            }
        }, ct).ConfigureAwait(false);

        _logger.LogInformation("Client-Bundle exportiert: {Count} Mod(s) → {Path}", activeMods.Count, targetZipPath);
        return targetZipPath;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsEnabledPak(string name) =>
        name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase);

    private static bool IsDisabledPak(string name) =>
        name.EndsWith($".pak{DisabledSuffix}", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> FindCompanions(string dir, string baseName)
    {
        var result = new List<string>();
        foreach (var ext in new[] { ".ucas", ".utoc" })
        {
            var candidate = $"{baseName}{ext}";
            if (File.Exists(Path.Combine(dir, candidate)))
                result.Add(candidate);
        }
        return result;
    }

    private static string BuildDisplayName(string baseName)
    {
        // "MyAwesome_Mod_v2" → "MyAwesome Mod v2"
        var cleaned = baseName.Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? baseName : cleaned;
    }

    private static List<string> CopyPakAndCompanions(string sourcePak, string destDir)
    {
        var name = Path.GetFileName(sourcePak);
        var target = Path.Combine(destDir, name);
        File.Copy(sourcePak, target, overwrite: true);

        var srcDir = Path.GetDirectoryName(sourcePak)!;
        var baseName = Path.GetFileNameWithoutExtension(name);
        foreach (var ext in new[] { ".ucas", ".utoc" })
        {
            var companionSrc = Path.Combine(srcDir, $"{baseName}{ext}");
            if (File.Exists(companionSrc))
            {
                var companionDst = Path.Combine(destDir, $"{baseName}{ext}");
                File.Copy(companionSrc, companionDst, overwrite: true);
            }
        }
        return [name];
    }

    public sealed class ScriptModOnlyException : InvalidOperationException
    {
        public ScriptModOnlyException() : base(
            "This archive contains only script files (Lua/SML) and no .pak. " +
            "Script mods only run in singleplayer — they have no effect on the server and were not installed.") { }
    }

    private List<string> ExtractZip(string zipPath, string destDir, CancellationToken ct)
    {
        var extracted = new List<string>();
        var allEntries = new List<string>();
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // Ordner-Einträge (FullName endet auf /) oder komplett leere Einträge überspringen
            var full = entry.FullName;
            if (string.IsNullOrWhiteSpace(full) || full.EndsWith('/') || full.EndsWith('\\'))
                continue;

            // entry.Name kann bei exotischen ZIPs leer sein — fallback auf FullName-basenname
            var fileName = !string.IsNullOrWhiteSpace(entry.Name)
                ? entry.Name
                : Path.GetFileName(full.Replace('\\', '/').TrimEnd('/'));
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            allEntries.Add(full);

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (Array.IndexOf(PakExtensions, ext) < 0) continue;

            var target = Path.Combine(destDir, fileName);
            using (var src = entry.Open())
            using (var dst = File.Create(target))
            {
                src.CopyTo(dst);
            }
            if (ext == ".pak") extracted.Add(fileName);
        }

        if (extracted.Count == 0 && allEntries.Count > 0)
        {
            _logger.LogWarning("ZIP {Path} enthielt keine .pak. Einträge: {Entries}",
                zipPath, string.Join(", ", allEntries.Take(20)));
            if (IsLikelyScriptMod(allEntries)) throw new ScriptModOnlyException();
        }
        return extracted;
    }

    private List<string> Extract7z(string archivePath, string destDir, CancellationToken ct)
    {
        var extracted = new List<string>();
        var allEntries = new List<string>();
        using var archive = SevenZipArchive.OpenArchive(archivePath);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.IsDirectory) continue;
            var entryName = entry.Key;
            if (string.IsNullOrWhiteSpace(entryName)) continue;

            var fileName = Path.GetFileName(entryName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            allEntries.Add(entryName);

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (Array.IndexOf(PakExtensions, ext) < 0) continue;

            var target = Path.Combine(destDir, fileName);
            using (var outStream = File.Create(target))
            {
                entry.WriteTo(outStream);
            }
            if (ext == ".pak") extracted.Add(fileName);
        }

        if (extracted.Count == 0 && allEntries.Count > 0)
        {
            _logger.LogWarning("7z {Path} enthielt keine .pak. Einträge: {Entries}",
                archivePath, string.Join(", ", allEntries.Take(20)));
            if (IsLikelyScriptMod(allEntries)) throw new ScriptModOnlyException();
        }
        return extracted;
    }

    private static bool IsLikelyScriptMod(IEnumerable<string> entries) =>
        entries.Any(e => e.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));
}
