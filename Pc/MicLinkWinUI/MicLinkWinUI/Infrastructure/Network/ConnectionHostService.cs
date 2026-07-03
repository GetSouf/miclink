namespace MicLinkWinUI.Infrastructure.Network;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using MicLinkWinUI.Infrastructure.Network.Protocol;

public sealed class ConnectionHostService : IConnectionHostService
{
    private readonly IPairingStore _pairingStore;
    private readonly ILogService _logService;
    private readonly AudioTcpServer _audioServer;
    private readonly MdnsHostAdvertiser _mdns = new();
    private readonly PairingTcpServer _tcpServer = new();
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private string _pairingPin = string.Empty;
    private PairedDeviceInfo? _activeSession;

    public ConnectionHostService(
        IPairingStore pairingStore,
        ILogService logService,
        AudioTcpServer audioServer)
    {
        _pairingStore = pairingStore;
        _logService = logService;
        _audioServer = audioServer;
        Status = ConnectionStatus.Disconnected;
        Telemetry = DeviceTelemetry.Empty;
    }

    public ConnectionStatus Status { get; private set; }

    public DeviceTelemetry Telemetry { get; private set; }

    public string PairingPin => _pairingPin;

    public bool IsPairingVisible => Status is ConnectionStatus.Disconnected or ConnectionStatus.Discovering;

    public event Action? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pairingPin = GeneratePin();
        _activeSession = _pairingStore.Load();

        _tcpServer.MessageReceived += HandleMessageAsync;
        _tcpServer.ClientDisconnected += OnClientDisconnected;

        await _tcpServer.StartAsync(AppConstants.DefaultPort, _cts.Token);
        await _audioServer.StartAsync(AppConstants.AudioPort, _cts.Token);
        await _mdns.StartAsync(Environment.MachineName, _cts.Token);

        SetStatus(ConnectionStatus.Discovering);
        _logService.Info($"Ожидание телефона. PIN: {_pairingPin}");
        _logService.Info($"mDNS: {AppConstants.MdnsServiceType}, порт {AppConstants.DefaultPort}");
        _logService.Info($"Аудио: порт {AppConstants.AudioPort}");
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _tcpServer.MessageReceived -= HandleMessageAsync;
        _tcpServer.ClientDisconnected -= OnClientDisconnected;
        await _tcpServer.DisposeAsync();
        await _audioServer.DisposeAsync();
        await _mdns.DisposeAsync();
        _cts?.Dispose();
        _cts = null;
        SetStatus(ConnectionStatus.Disconnected);
    }

    private Task<string?> HandleMessageAsync(string line)
    {
        var message = ProtocolJson.Deserialize(line);
        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            return Task.FromResult<string?>(null);
        }

        return message.Type switch
        {
            MicLinkProtocol.MessageTypes.PairRequest => Task.FromResult(HandlePairRequest(message)),
            MicLinkProtocol.MessageTypes.ReconnectRequest => Task.FromResult(HandleReconnectRequest(message)),
            MicLinkProtocol.MessageTypes.Heartbeat => Task.FromResult(HandleHeartbeat(message)),
            MicLinkProtocol.MessageTypes.MuteUpdate => Task.FromResult(HandleMuteUpdate(message)),
            _ => Task.FromResult<string?>(null)
        };
    }

    private string? HandlePairRequest(ProtocolMessage message)
    {
        if (message.Pin != _pairingPin)
        {
            _logService.Warning("Неверный PIN при сопряжении");
            return ProtocolJson.Serialize(new ProtocolMessage
            {
                Type = MicLinkProtocol.MessageTypes.PairResponse,
                Success = false,
                Error = "invalid_pin"
            });
        }

        var token = Guid.NewGuid().ToString("N");
        var device = new PairedDeviceInfo
        {
            DeviceId = message.DeviceId ?? Guid.NewGuid().ToString("N"),
            DeviceName = message.DeviceName ?? "Android",
            Token = token
        };

        lock (_gate)
        {
            _activeSession = device;
        }

        _pairingStore.Save(device);
        UpdateTelemetry(device.DeviceName, new DeviceTelemetry
        {
            DeviceName = device.DeviceName,
            Transport = TransportMode.WiFi
        });
        SetStatus(ConnectionStatus.Connected);
        _logService.Info($"Сопряжено с {device.DeviceName}");

        return ProtocolJson.Serialize(new ProtocolMessage
        {
            Type = MicLinkProtocol.MessageTypes.PairResponse,
            Success = true,
            Token = token,
            PcName = Environment.MachineName,
            AudioPort = AppConstants.AudioPort
        });
    }

    private string? HandleReconnectRequest(ProtocolMessage message)
    {
        var saved = _pairingStore.Load();
        if (saved is null || saved.Token != message.Token || saved.DeviceId != message.DeviceId)
        {
            _logService.Warning("Не удалось переподключить устройство");
            return ProtocolJson.Serialize(new ProtocolMessage
            {
                Type = MicLinkProtocol.MessageTypes.ReconnectResponse,
                Success = false,
                Error = "invalid_token"
            });
        }

        lock (_gate)
        {
            _activeSession = new PairedDeviceInfo
            {
                DeviceId = saved.DeviceId,
                DeviceName = message.DeviceName ?? saved.DeviceName,
                Token = saved.Token
            };
        }

        UpdateTelemetry(_activeSession.DeviceName, Telemetry);
        SetStatus(ConnectionStatus.Connected);
        _logService.Info($"Переподключено: {_activeSession.DeviceName}");

        return ProtocolJson.Serialize(new ProtocolMessage
        {
            Type = MicLinkProtocol.MessageTypes.ReconnectResponse,
            Success = true,
            PcName = Environment.MachineName,
            AudioPort = AppConstants.AudioPort
        });
    }

    private string? HandleHeartbeat(ProtocolMessage message)
    {
        if (!IsAuthorized(message.Token))
        {
            return null;
        }

        var deviceName = _activeSession?.DeviceName ?? "Android";
        UpdateTelemetry(deviceName, new DeviceTelemetry
        {
            DeviceName = deviceName,
            BatteryPercent = message.Battery ?? -1,
            SignalStrength = message.Signal ?? -1,
            IsMicrophoneMuted = message.MicMuted ?? false,
            IsCameraMuted = message.CameraMuted ?? false,
            PingMs = message.PingMs ?? -1,
            Transport = TransportMode.WiFi
        });

        return ProtocolJson.Serialize(new ProtocolMessage
        {
            Type = MicLinkProtocol.MessageTypes.HeartbeatAck,
            PingMs = message.PingMs
        });
    }

    private string? HandleMuteUpdate(ProtocolMessage message)
    {
        if (!IsAuthorized(message.Token))
        {
            return null;
        }

        UpdateTelemetry(Telemetry.DeviceName, Telemetry with
        {
            IsMicrophoneMuted = message.MicMuted ?? Telemetry.IsMicrophoneMuted,
            IsCameraMuted = message.CameraMuted ?? Telemetry.IsCameraMuted
        });

        return null;
    }

    private bool IsAuthorized(string? token)
    {
        lock (_gate)
        {
            return _activeSession is not null && _activeSession.Token == token;
        }
    }

    private void OnClientDisconnected()
    {
        var wasConnected = Status == ConnectionStatus.Connected;
        Telemetry = DeviceTelemetry.Empty;

        lock (_gate)
        {
            _activeSession = _pairingStore.Load();
        }

        if (wasConnected)
        {
            _pairingPin = GeneratePin();
            _logService.Warning("Телефон отключился");
            _logService.Info($"Новый PIN: {_pairingPin}");
        }

        SetStatus(ConnectionStatus.Discovering);
    }

    private void SetStatus(ConnectionStatus status)
    {
        Status = status;
        NotifyStateChanged();
    }

    private void UpdateTelemetry(string deviceName, DeviceTelemetry telemetry)
    {
        Telemetry = telemetry with { DeviceName = deviceName };
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private static string GeneratePin() => Random.Shared.Next(100_000, 999_999).ToString();
}
