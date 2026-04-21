using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.Core.Tests.TestDoubles;

/// <summary>Minimal no-op IAppSettingsService for unit tests that don't need settings state.</summary>
internal sealed class NullAppSettingsService : IAppSettingsService
{
    public static readonly NullAppSettingsService Instance = new();

    public AppSettings Current { get; } = new();
    public string ActiveServerDir => string.Empty;

    public event Action<AppSettings>? Changed { add { } remove { } }

    public Task SelectServerAsync(string serverId) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
    {
        mutate(Current);
        return Task.CompletedTask;
    }
}
