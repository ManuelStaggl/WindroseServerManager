using WindroseServerManager.Core.Models;

namespace WindroseServerManager.App.Services;

public interface IThemeService
{
    void Apply(ThemeMode mode);
}
