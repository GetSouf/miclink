namespace MicLinkWinUI.Domain.Interfaces;

public interface IAudioPlaybackService
{
    float InputLevel { get; }
    bool IsActive { get; }

    event Action? LevelChanged;

    void Start();
    void Stop();

    /// <summary>Flushes jitter buffers when a new audio TCP session starts without reopening the driver.</summary>
    void ResetForNewStream();

    void PushPcm(ReadOnlySpan<byte> pcm);
}
