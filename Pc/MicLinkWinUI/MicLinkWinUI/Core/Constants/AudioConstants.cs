namespace MicLinkWinUI.Core.Constants;

public static class AudioConstants
{
    public const int SampleRate = 48_000;
    public const int Channels = 1;
    public const int BitsPerSample = 16;
    public const int FrameDurationMs = 20;
    public const int SamplesPerFrame = SampleRate * FrameDurationMs / 1000;
    public const int BytesPerFrame = SamplesPerFrame * Channels * (BitsPerSample / 8);
}
