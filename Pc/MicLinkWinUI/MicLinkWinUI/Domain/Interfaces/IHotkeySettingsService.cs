namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Models;

public interface IHotkeySettingsService
{
    HotkeySettings Current { get; }

    event Action? SettingsChanged;

    void Save(HotkeySettings settings);
}
