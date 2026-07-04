namespace MicLinkWinUI.Infrastructure.Network;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MicLinkWinUI.Domain.Interfaces;

public sealed class AudioTcpServer : IAsyncDisposable
{
    private readonly IAudioPlaybackService _playback;
    private readonly ILogService _logService;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public AudioTcpServer(IAudioPlaybackService playback, ILogService logService)
    {
        _playback = playback;
        _logService = logService;
    }

    public Task StartAsync(int port, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
                client.ReceiveBufferSize = 8192;
                client.SendBufferSize = 8192;
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        _logService.Info("Аудиопоток подключён");

        if (!_playback.IsActive)
        {
            _playback.Start();
        }
        else
        {
            _playback.ResetForNewStream();
        }

        try
        {
            await using var stream = client.GetStream();
            var lengthBuffer = new byte[4];
            var payloadBuffer = new byte[8192];

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                await ReadExactAsync(stream, lengthBuffer, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

                if (length <= 0 || length > payloadBuffer.Length)
                {
                    _logService.Warning($"Некорректный аудиокадр: {length} байт");
                    break;
                }

                if (length > payloadBuffer.Length)
                {
                    Array.Resize(ref payloadBuffer, length);
                }

                await ReadExactAsync(stream, payloadBuffer.AsMemory(0, length), cancellationToken);
                _playback.PushPcm(payloadBuffer.AsSpan(0, length));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _playback.ResetForNewStream();
            client.Dispose();
            _logService.Info("Аудиопоток остановлен");
        }
    }

    private static async Task ReadExactAsync(
        NetworkStream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken);
            if (read == 0)
            {
                throw new IOException("Соединение закрыто");
            }

            total += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _playback.Stop();
        _cts?.Dispose();
        await Task.CompletedTask;
    }
}
