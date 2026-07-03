namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Models;

public interface IConnectionHostService
{
    ConnectionStatus Status { get; }
    DeviceTelemetry Telemetry { get; }
    string PairingPin { get; }
    bool IsPairingVisible { get; }

    event Action? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
