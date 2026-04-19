namespace WindroseServerManager.Core.Tests.Fixtures;

public sealed class TempServerFixture : IDisposable
{
    public string ServerDir { get; }
    public string CacheDir { get; }
    public string RootDir { get; }

    public TempServerFixture()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "wsm-tests", Guid.NewGuid().ToString("N"));
        ServerDir = Path.Combine(RootDir, "server");
        CacheDir = Path.Combine(RootDir, "cache");
        Directory.CreateDirectory(ServerDir);
        Directory.CreateDirectory(CacheDir);
        // Seed a realistic-looking server layout so FindServerBinary works.
        Directory.CreateDirectory(Path.Combine(ServerDir, "R5", "Binaries", "Win64"));
        File.WriteAllText(Path.Combine(ServerDir, "WindroseServer.exe"), "fake exe");
    }

    public void SeedExistingUserConfig(string fileRelPath, string content)
    {
        var full = Path.Combine(ServerDir, fileRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Dispose()
    {
        try { Directory.Delete(RootDir, recursive: true); } catch { /* best effort */ }
    }
}
