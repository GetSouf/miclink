namespace MicLinkWinUI.Domain.Models;

public sealed class PairedDeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}
