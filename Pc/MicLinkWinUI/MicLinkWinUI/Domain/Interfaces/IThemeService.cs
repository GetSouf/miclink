namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Models;

public interface IThemeService
{
    ThemeSettings Current { get; }
    event EventHandler<ThemeSettings>? ThemeChanged;
    void Apply(ThemeSettings settings);
    void Save(ThemeSettings settings);
    ThemeSettings Load();
}
