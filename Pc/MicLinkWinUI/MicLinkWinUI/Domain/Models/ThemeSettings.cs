namespace MicLinkWinUI.Domain.Models;

using MicLinkWinUI.Domain.Enums;

public sealed class ThemeSettings
{
    public AppThemeMode Mode { get; init; } = AppThemeMode.Dark;
    public string AccentColorHex { get; init; } = "#6C5CE7";
}
