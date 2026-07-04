namespace MicLinkWinUI.Infrastructure.Theming;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using MicLinkWinUI.Infrastructure.Storage;

public sealed class ThemeService : IThemeService
{
    private readonly LocalSettingsStore _store;

    public ThemeSettings Current { get; private set; }

    public event EventHandler<ThemeSettings>? ThemeChanged;

    public ThemeService(LocalSettingsStore store)
    {
        _store = store;
        Current = Load();
        ThemeApplicator.Apply(Current);
    }

    public void Apply(ThemeSettings settings)
    {
        Current = settings;
        ThemeApplicator.Apply(settings);
        ThemeChanged?.Invoke(this, settings);
    }

    public void Save(ThemeSettings settings)
    {
        _store.Set(AppConstants.SettingsKeyTheme, settings.Mode.ToString());
        Apply(settings);
    }

    public ThemeSettings Load()
    {
        var modeValue = _store.Get(AppConstants.SettingsKeyTheme);

        var mode = Enum.TryParse<AppThemeMode>(modeValue, out var parsed)
            ? parsed
            : AppThemeMode.Dark;

        return new ThemeSettings { Mode = mode };
    }
}
