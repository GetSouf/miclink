namespace MicLinkWinUI.Domain.Interfaces;

public interface IAudioPlaybackService
{
    float InputLevel { get; }
    bool IsActive { get; }

    event Action? LevelChanged;

    void Start();
    void Stop();
    void PushPcm(ReadOnlySpan<byte> pcm);
}
