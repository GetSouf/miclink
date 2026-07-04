namespace MicLinkWinUI.Infrastructure.Audio;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;

/// <summary>
/// Smooths irregular TCP chunks into steady writes to the virtual mic driver.
///
/// Discord/WASAPI reads at a fixed rate from a ~40 ms kernel ring. TCP arrives in
/// bursts. Without this buffer, bursts → ring underrun → silence gaps → stutter.
///
/// Do NOT reduce PrefillMs, add latency trimming, or inject silence frames here —
/// those changes reintroduce stutter. Latency is controlled only by monitor buffer.
/// </summary>
internal sealed class DriverPcmFeedBuffer : IDisposable
{
    /// <summary>Proven stable prefill — do not lower without measuring Discord underruns.</summary>
    private const int PrefillMs = 120;

    private const int FeedIntervalMs = 10;
    private const int FeedMs = 20;
    private const int BytesPerMs = AudioConstants.SampleRate * 2 / 1000;
    private static readonly int PrefillBytes = BytesPerMs * PrefillMs;
    private static readonly int FeedChunkBytes = BytesPerMs * FeedMs;
    private static readonly int CapacityBytes = AudioConstants.SampleRate * 2 * 4;

    private readonly IVirtualMicDriverService _driver;
    private readonly object _gate = new();
    private readonly byte[] _buffer = new byte[CapacityBytes];
    private readonly byte[] _feedScratch = new byte[FeedChunkBytes];

    private int _writePos;
    private int _readPos;
    private int _available;
    private bool _primed;
    private Timer? _timer;

    public DriverPcmFeedBuffer(IVirtualMicDriverService driver)
    {
        _driver = driver;
    }

    public void Start()
    {
        lock (_gate)
        {
            ResetBuffer();
            _timer?.Dispose();
            _timer = new Timer(FeedTick, null, FeedIntervalMs, FeedIntervalMs);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            ResetBuffer();
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            ResetBuffer();
        }
    }

    public void Enqueue(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            for (var i = 0; i < pcm.Length; ++i)
            {
                if (_available == _buffer.Length)
                {
                    _readPos = (_readPos + 1) % _buffer.Length;
                    _available--;
                }

                _buffer[_writePos] = pcm[i];
                _writePos = (_writePos + 1) % _buffer.Length;
                _available++;
            }
        }
    }

    private void FeedTick(object? _)
    {
        byte[] chunk;
        int length;

        lock (_gate)
        {
            if (_timer is null)
            {
                return;
            }

            if (!_primed)
            {
                if (_available < PrefillBytes)
                {
                    return;
                }

                _primed = true;
            }

            length = Math.Min(FeedChunkBytes, _available);
            if (length == 0)
            {
                return;
            }

            for (var i = 0; i < length; ++i)
            {
                _feedScratch[i] = _buffer[_readPos];
                _readPos = (_readPos + 1) % _buffer.Length;
            }

            _available -= length;
            chunk = _feedScratch;
        }

        if (!_driver.TryWritePcm(chunk.AsSpan(0, length)))
        {
            lock (_gate)
            {
                _primed = false;
            }
        }
    }

    private void ResetBuffer()
    {
        _writePos = 0;
        _readPos = 0;
        _available = 0;
        _primed = false;
    }

    public void Dispose()
    {
        Stop();
    }
}
