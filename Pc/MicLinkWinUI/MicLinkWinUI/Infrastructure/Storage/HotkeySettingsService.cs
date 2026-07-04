namespace MicLinkWinUI.Infrastructure.Storage;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;

public sealed class HotkeySettingsService : IHotkeySettingsService
{
    private readonly LocalSettingsStore _store;

    public HotkeySettingsService(LocalSettingsStore store)
    {
        _store = store;
        Current = Load();
    }

    public HotkeySettings Current { get; private set; }

    public event Action? SettingsChanged;

    public void Save(HotkeySettings settings)
    {
        _store.Set(AppConstants.SettingsKeyHotkeyMicMute, settings.MicrophoneMute);
        _store.Set(AppConstants.SettingsKeyHotkeyCameraMute, settings.CameraMute);
        Current = settings;
        SettingsChanged?.Invoke();
    }

    private HotkeySettings Load() =>
        new()
        {
            MicrophoneMute = _store.Get(AppConstants.SettingsKeyHotkeyMicMute) ?? string.Empty,
            CameraMute = _store.Get(AppConstants.SettingsKeyHotkeyCameraMute) ?? string.Empty,
        };
}
