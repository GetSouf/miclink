namespace MicLinkWinUI.Infrastructure.Network;

using System.Net;
using System.Net.Sockets;
using System.Text;

public sealed class PairingTcpServer : IAsyncDisposable
{
    private readonly object _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private TcpClient? _activeClient;
    private NetworkStream? _activeStream;
    private StreamWriter? _activeWriter;

    public event Func<string, Task<string?>>? MessageReceived;
    public event Action? ClientDisconnected;

    public bool HasActiveClient
    {
        get
        {
            lock (_gate)
            {
                return _activeClient?.Connected == true;
            }
        }
    }

    public async Task StartAsync(int port, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        _ = AcceptLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task SendAsync(string line, CancellationToken cancellationToken = default)
    {
        StreamWriter? writer;
        lock (_gate)
        {
            writer = _activeWriter;
        }

        if (writer is null)
        {
            return;
        }

        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                AttachClient(client);
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

    private void AttachClient(TcpClient client)
    {
        lock (_gate)
        {
            _activeClient?.Dispose();
            _activeClient = client;
            _activeStream = client.GetStream();
            _activeWriter = new StreamWriter(_activeStream, Encoding.UTF8) { AutoFlush = true };
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (MessageReceived is null)
                {
                    continue;
                }

                var response = await MessageReceived(line);
                if (response is not null)
                {
                    await SendAsync(response, cancellationToken);
                }
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
            var shouldNotify = false;
            lock (_gate)
            {
                if (ReferenceEquals(_activeClient, client))
                {
                    _activeClient = null;
                    _activeStream = null;
                    _activeWriter = null;
                    shouldNotify = true;
                }
            }

            client.Dispose();
            if (shouldNotify)
            {
                ClientDisconnected?.Invoke();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();

        lock (_gate)
        {
            _activeWriter?.Dispose();
            _activeClient?.Dispose();
            _activeWriter = null;
            _activeClient = null;
            _activeStream = null;
        }

        _cts?.Dispose();
        await Task.CompletedTask;
    }
}
