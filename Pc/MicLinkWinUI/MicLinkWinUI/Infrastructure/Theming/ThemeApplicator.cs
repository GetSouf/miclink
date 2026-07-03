namespace MicLinkWinUI.Infrastructure.Theming;

using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

public static class ThemeApplicator
{
    public static void Apply(ThemeSettings settings, FrameworkElement? root = null)
    {
        if (Application.Current is null)
        {
            return;
        }

        // RequestedTheme on Application.Current throws COMException in unpackaged WinUI 3.
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

        Application.Current.Resources["AccentBrush"] =
            new SolidColorBrush(ParseColor(settings.AccentColorHex));
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        var value = Convert.ToUInt32(hex, 16);
        return Windows.UI.Color.FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }
}
