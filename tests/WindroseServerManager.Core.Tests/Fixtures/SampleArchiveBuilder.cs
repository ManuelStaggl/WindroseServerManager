using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace WindroseServerManager.Core.Tests.Fixtures;

public static class SampleArchiveBuilder
{
    /// <summary>Builds a zip mirroring WindrosePlus v1.0.6 layout, returns (bytes, sha256Hex). Entries:
    /// StartWindrosePlusServer.bat, LICENSE, UE4SS-settings.ini, WindrosePlus/config/windrose_plus.default.ini,
    /// install.ps1, README.md. LICENSE must contain the exact string "MIT License".</summary>
    public static (byte[] Bytes, string Sha256Hex) BuildWindrosePlusZip(string tag = "v1.0.6")
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "StartWindrosePlusServer.bat", $"@echo off\r\nREM WindrosePlus {tag} launcher\r\nWindroseServer.exe %*\r\n");
            WriteEntry(archive, "LICENSE", "MIT License\r\n\r\nCopyright (c) 2025 HumanGenome\r\n\r\nPermission is hereby granted, free of charge...\r\n");
            WriteEntry(archive, "UE4SS-settings.ini", "[General]\r\nEnableHotReloadSystem=1\r\n");
            WriteEntry(archive, "WindrosePlus/config/windrose_plus.default.ini", "[WindrosePlus]\r\nVersion=" + tag + "\r\n");
            WriteEntry(archive, "install.ps1", "# vendor install script\r\nWrite-Host 'installed'\r\n");
            WriteEntry(archive, "README.md", $"# WindrosePlus {tag}\r\nSee LICENSE for terms.\r\n");
        }
        var bytes = ms.ToArray();
        return (bytes, Sha256Hex(bytes));
    }

    /// <summary>Builds a zip mirroring UE4SS release layout. Entries: dwmapi.dll, ue4ss/UE4SS.dll, ue4ss/Mods/keep_me.txt.</summary>
    public static (byte[] Bytes, string Sha256Hex) BuildUe4ssZip(string tag = "experimental-latest")
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "dwmapi.dll", "MZ-fake-ue4ss-proxy-dll");
            WriteEntry(archive, "ue4ss/UE4SS.dll", "MZ-fake-ue4ss-core-dll-" + tag);
            WriteEntry(archive, "ue4ss/Mods/keep_me.txt", "keep");
        }
        var bytes = ms.ToArray();
        return (bytes, Sha256Hex(bytes));
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var s = entry.Open();
        var buffer = Encoding.UTF8.GetBytes(content);
        s.Write(buffer, 0, buffer.Length);
    }
}
