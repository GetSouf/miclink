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
    private readonly IAudioEffectsService _effects;
    private readonly DriverPcmFeedBuffer _driverFeed;
    private readonly object _gate = new();

    private WasapiOut? _monitorOutput;
    private BufferedWaveProvider? _monitorBuffer;
    private bool _virtualMicOpen;
    private byte[] _processedBuffer = [];

    public PcmPlaybackService(
        IAudioOutputSettingsService outputSettings,
        IVirtualMicDriverService virtualMicDriver,
        IAudioEffectsService effects)
    {
        _outputSettings = outputSettings;
        _virtualMicDriver = virtualMicDriver;
        _effects = effects;
        _driverFeed = new DriverPcmFeedBuffer(virtualMicDriver);
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
            if (_virtualMicOpen)
            {
                _driverFeed.Start();
            }

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

    public void ResetForNewStream()
    {
        lock (_gate)
        {
            _driverFeed.Flush();
            _monitorBuffer?.ClearBuffer();
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

        var processed = ProcessEffects(pcm);

        if (feedOpen)
        {
            _driverFeed.Enqueue(processed);
        }

        if (monitorBuffer is not null)
        {
            monitorBuffer.AddSamples(processed.ToArray(), 0, processed.Length);
        }

        UpdateLevel(processed);
    }

    private ReadOnlySpan<byte> ProcessEffects(ReadOnlySpan<byte> pcm)
    {
        if (!_effects.HasActiveProcessors)
        {
            return pcm;
        }

        if (_processedBuffer.Length < pcm.Length)
        {
            _processedBuffer = new byte[Math.Max(pcm.Length, _processedBuffer.Length * 2)];
        }

        _effects.Process(pcm, _processedBuffer.AsSpan(0, pcm.Length));
        return _processedBuffer.AsSpan(0, pcm.Length);
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
        _driverFeed.Stop();
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
        BufferDuration = TimeSpan.FromMilliseconds(250),
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
        _driverFeed.Dispose();
    }
}

