namespace MicLinkWinUI.Domain.Interfaces;

public interface IAudioEffect
{
    string SlotId { get; }

    string DisplayName { get; }

    bool IsEnabled { get; set; }

    bool IsFunctional { get; }

    IReadOnlyDictionary<string, float> Parameters { get; }

    void SetParameter(string key, float value);

    void Process(Span<short> samples, int sampleRate);
}
