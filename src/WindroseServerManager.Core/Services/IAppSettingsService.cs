using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }
    event Action<AppSettings>? Changed;
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default);
}
