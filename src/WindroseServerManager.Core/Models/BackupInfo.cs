namespace WindroseServerManager.Core.Models;

public sealed record BackupInfo(
    string FileName,
    string FullPath,
    DateTime CreatedUtc,
    long SizeBytes,
    bool IsAutomatic);
