namespace MicLinkWinUI.Presentation.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly IConnectionHostService _connectionHost;
    private readonly IAudioPlaybackService _audioPlayback;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private float _audioInputLevel;

    [ObservableProperty]
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    [ObservableProperty]
    private DeviceTelemetry _telemetry = DeviceTelemetry.Empty;

    [ObservableProperty]
    private string _statusText = "Ожидание подключения";

    [ObservableProperty]
    private string _pairingPin = string.Empty;

    [ObservableProperty]
    private bool _isPairingVisible;

    [ObservableProperty]
    private bool _isSpectrumVisualizerEnabled = true;

    public MainViewModel(
        ILogService logService,
        IConnectionHostService connectionHost,
        IAudioPlaybackService audioPlayback)
    {
        _logService = logService;
        _connectionHost = connectionHost;
        _audioPlayback = audioPlayback;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _connectionHost.StateChanged += SyncFromHost;
        _audioPlayback.LevelChanged += OnAudioLevelChanged;
        _logService.Info($"{AppConstants.AppName} запущен");

        SyncFromHost();
        _ = _connectionHost.StartAsync();
    }

    public Visibility PairingPinVisibility =>
        IsPairingVisible ? Visibility.Visible : Visibility.Collapsed;

    public string PairingPinDisplay =>
        PairingPin.Length == 6
            ? $"{PairingPin[..3]} {PairingPin[3..]}"
            : PairingPin;

    public string StatusBadgeColor => Status switch
    {
        ConnectionStatus.Connected => "#00B894",
        ConnectionStatus.Discovering or ConnectionStatus.Pairing or ConnectionStatus.Reconnecting => "#FDCB6E",
        ConnectionStatus.Error => "#E17055",
        _ => "#636E72"
    };

    public string BatteryDisplay =>
        Telemetry.BatteryPercent >= 0 ? $"{Telemetry.BatteryPercent}%" : "—";

    public string SignalDisplay =>
        Telemetry.SignalStrength >= 0 ? $"{Telemetry.SignalStrength}%" : "—";

    public string PingDisplay =>
        Telemetry.PingMs >= 0 ? $"{Telemetry.PingMs} ms" : "—";

    public string MicStatusDisplay =>
        Telemetry.IsMicrophoneMuted ? "Выкл" : "Вкл";

    public string CameraStatusDisplay =>
        Telemetry.IsCameraMuted ? "Выкл" : "Вкл";

    public bool IsMicrophoneMuted => Telemetry.IsMicrophoneMuted;

    public bool IsCameraMuted => Telemetry.IsCameraMuted;

    public bool CanControlMute => Status == ConnectionStatus.Connected;

    public string AudioLevelHint =>
        Telemetry.IsMicrophoneMuted
            ? "Микрофон выключен"
            : AudioInputLevel > 1
                ? "Приём аудио"
                : "Говорите в телефон";

    [RelayCommand(CanExecute = nameof(CanControlMute))]
    private async Task ToggleMicrophoneMuteAsync()
    {
        await _connectionHost.SetMicrophoneMutedAsync(!Telemetry.IsMicrophoneMuted);
    }

    [RelayCommand(CanExecute = nameof(CanControlMute))]
    private async Task ToggleCameraMuteAsync()
    {
        await _connectionHost.SetCameraMutedAsync(!Telemetry.IsCameraMuted);
    }

    [RelayCommand]
    private void ToggleSpectrumVisualizer()
    {
        IsSpectrumVisualizerEnabled = !IsSpectrumVisualizerEnabled;
    }

    private void OnAudioLevelChanged()
    {
        _dispatcher.TryEnqueue(() =>
        {
            AudioInputLevel = _audioPlayback.InputLevel;
        });
    }

    private void SyncFromHost()
    {
        _dispatcher.TryEnqueue(() =>
        {
            Status = _connectionHost.Status;
            Telemetry = _connectionHost.Telemetry;
            PairingPin = _connectionHost.PairingPin;
            IsPairingVisible = _connectionHost.IsPairingVisible;

            OnPropertyChanged(nameof(PairingPinDisplay));
            OnPropertyChanged(nameof(PairingPinVisibility));
            OnPropertyChanged(nameof(BatteryDisplay));
            OnPropertyChanged(nameof(SignalDisplay));
            OnPropertyChanged(nameof(PingDisplay));
            OnPropertyChanged(nameof(MicStatusDisplay));
            OnPropertyChanged(nameof(CameraStatusDisplay));
            OnPropertyChanged(nameof(IsMicrophoneMuted));
            OnPropertyChanged(nameof(IsCameraMuted));
            OnPropertyChanged(nameof(CanControlMute));
            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(AudioLevelHint));
            ToggleMicrophoneMuteCommand.NotifyCanExecuteChanged();
            ToggleCameraMuteCommand.NotifyCanExecuteChanged();
        });
    }

    partial void OnStatusChanged(ConnectionStatus value)
    {
        StatusText = value switch
        {
            ConnectionStatus.Connected => "Подключено",
            ConnectionStatus.Discovering => "Ожидание телефона",
            ConnectionStatus.Pairing => "Сопряжение…",
            ConnectionStatus.Reconnecting => "Переподключение…",
            ConnectionStatus.Error => "Ошибка подключения",
            _ => "Ожидание подключения"
        };

        OnPropertyChanged(nameof(CanControlMute));
        OnPropertyChanged(nameof(StatusBadgeColor));
        ToggleMicrophoneMuteCommand.NotifyCanExecuteChanged();
        ToggleCameraMuteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPairingVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(PairingPinVisibility));

    partial void OnTelemetryChanged(DeviceTelemetry value)
    {
        OnPropertyChanged(nameof(BatteryDisplay));
        OnPropertyChanged(nameof(SignalDisplay));
        OnPropertyChanged(nameof(PingDisplay));
        OnPropertyChanged(nameof(MicStatusDisplay));
        OnPropertyChanged(nameof(CameraStatusDisplay));
        OnPropertyChanged(nameof(IsMicrophoneMuted));
        OnPropertyChanged(nameof(IsCameraMuted));
        OnPropertyChanged(nameof(AudioLevelHint));
    }
}
