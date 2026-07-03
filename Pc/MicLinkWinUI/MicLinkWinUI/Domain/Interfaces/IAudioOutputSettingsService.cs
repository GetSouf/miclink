namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Models;

public interface IAudioOutputSettingsService
{
    AudioOutputSettings Current { get; }
    event Action? SettingsChanged;
    void Save(AudioOutputSettings settings);
    AudioOutputSettings Load();
}
