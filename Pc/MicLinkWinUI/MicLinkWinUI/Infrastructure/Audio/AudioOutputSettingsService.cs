namespace MicLinkWinUI.Infrastructure.Audio;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using MicLinkWinUI.Infrastructure.Storage;

public sealed class AudioOutputSettingsService : IAudioOutputSettingsService
{
    private readonly LocalSettingsStore _store;

    public AudioOutputSettingsService(LocalSettingsStore store)
    {
        _store = store;
        Current = Load();
    }

    public AudioOutputSettings Current { get; private set; }

    public event Action? SettingsChanged;

    public void Save(AudioOutputSettings settings)
    {
        _store.Set(AppConstants.SettingsKeyMonitorSpeakers, settings.MonitorOnSpeakers.ToString());
        Current = settings;
        SettingsChanged?.Invoke();
    }

    public AudioOutputSettings Load()
    {
        var monitorValue = _store.Get(AppConstants.SettingsKeyMonitorSpeakers);
        var monitor = !bool.TryParse(monitorValue, out var parsed) || parsed;

        return new AudioOutputSettings
        {
            MonitorOnSpeakers = monitor
        };
    }
}
