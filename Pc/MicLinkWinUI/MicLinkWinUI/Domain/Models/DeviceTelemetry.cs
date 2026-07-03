namespace MicLinkWinUI.Domain.Models;

using MicLinkWinUI.Domain.Enums;

public sealed record DeviceTelemetry
{
    public string DeviceName { get; init; } = "—";
    public int BatteryPercent { get; init; } = -1;
    public int SignalStrength { get; init; } = -1;
    public bool IsMicrophoneMuted { get; init; }
    public bool IsCameraMuted { get; init; }
    public int PingMs { get; init; } = -1;
    public TransportMode Transport { get; init; } = TransportMode.WiFi;

    public static DeviceTelemetry Empty { get; } = new();
}
