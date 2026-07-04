namespace MicLinkWinUI.Infrastructure.Theming;

using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Models;
using Microsoft.UI.Xaml;

public static class ThemeApplicator
{
    public static void Apply(ThemeSettings settings, FrameworkElement? root = null)
    {
        if (Application.Current is null)
        {
            return;
        }

        var elementTheme = settings.Mode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (root is not null)
        {
            root.RequestedTheme = elementTheme;
        }
    }
}
