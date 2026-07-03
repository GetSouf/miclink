namespace MicLinkWinUI.Infrastructure.Audio;

using System.Buffers.Binary;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;

public sealed class PcmPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly IAudioOutputSettingsService _outputSettings;
    private readonly IVirtualMicDriverService _virtualMicDriver;
    private readonly object _gate = new();

    private WasapiOut? _monitorOutput;
    private BufferedWaveProvider? _monitorBuffer;
    private bool _virtualMicOpen;

    public PcmPlaybackService(
        IAudioOutputSettingsService outputSettings,
        IVirtualMicDriverService virtualMicDriver)
    {
        _outputSettings = outputSettings;
        _virtualMicDriver = virtualMicDriver;
        _outputSettings.SettingsChanged += OnOutputSettingsChanged;
    }

    public float InputLevel { get; private set; }

    public bool IsActive { get; private set; }

    public event Action? LevelChanged;

    public void Start()
    {
        lock (_gate)
        {
            StopInternal();

            _virtualMicDriver.RefreshStatus();
            _virtualMicOpen = _virtualMicDriver.TryOpenFeed();

            var format = CreateFormat();
            var settings = _outputSettings.Current;

            if (settings.MonitorOnSpeakers)
            {
                _monitorBuffer = CreateBuffer(format);
                _monitorOutput = CreateMonitorOutput(_monitorBuffer);
                _monitorOutput.Play();
            }

            IsActive = _virtualMicOpen || _monitorOutput is not null;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopInternal();
        }
    }

    public void PushPcm(ReadOnlySpan<byte> pcm)
    {
        BufferedWaveProvider? monitorBuffer;
        var feedOpen = false;

        lock (_gate)
        {
            monitorBuffer = _monitorBuffer;
            feedOpen = _virtualMicOpen;
        }

        if (feedOpen && !_virtualMicDriver.TryWritePcm(pcm))
        {
            lock (_gate)
            {
                _virtualMicOpen = false;
            }
        }

        if (monitorBuffer is not null)
        {
            var pcmArray = pcm.ToArray();
            monitorBuffer.AddSamples(pcmArray, 0, pcmArray.Length);
        }

        UpdateLevel(pcm);
    }

    private void OnOutputSettingsChanged()
    {
        if (!IsActive)
        {
            return;
        }

        Start();
    }

    private void StopInternal()
    {
        _virtualMicDriver.CloseFeed();
        _virtualMicOpen = false;

        _monitorOutput?.Stop();
        _monitorOutput?.Dispose();
        _monitorOutput = null;
        _monitorBuffer = null;
        IsActive = false;
        InputLevel = 0;
        LevelChanged?.Invoke();
    }

    private static WaveFormat CreateFormat() => new(
        AudioConstants.SampleRate,
        AudioConstants.BitsPerSample,
        AudioConstants.Channels);

    private static BufferedWaveProvider CreateBuffer(WaveFormat format) => new(format)
    {
        BufferDuration = TimeSpan.FromSeconds(2),
        DiscardOnBufferOverflow = true
    };

    private static WasapiOut CreateMonitorOutput(BufferedWaveProvider buffer)
    {
        var output = new WasapiOut(AudioClientShareMode.Shared, 50);
        output.Init(buffer);
        return output;
    }

    private void UpdateLevel(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length < 2)
        {
            return;
        }

        double sum = 0;
        var sampleCount = pcm.Length / 2;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i, 2));
            sum += sample * sample;
        }

        var rms = Math.Sqrt(sum / sampleCount);
        InputLevel = (float)Math.Clamp(rms / short.MaxValue * 100, 0, 100);
        LevelChanged?.Invoke();
    }

    public void Dispose()
    {
        _outputSettings.SettingsChanged -= OnOutputSettingsChanged;
        Stop();
    }
}
