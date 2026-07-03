namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Enums;

public interface IVirtualMicDriverService
{
    VirtualMicDriverStatus Status { get; }

    string CaptureDeviceName { get; }

    bool HasBundledDriverPackage { get; }

    event Action? StatusChanged;

    void RefreshStatus();

    bool IsCaptureDevicePresent();

    bool TryOpenFeed();

    void CloseFeed();

    bool TryWritePcm(ReadOnlySpan<byte> pcm);

    Task<bool> InstallDriverAsync();

    Task<bool> EnsureReadyAsync();
}
