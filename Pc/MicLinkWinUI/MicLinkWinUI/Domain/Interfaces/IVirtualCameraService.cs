namespace MicLinkWinUI.Domain.Interfaces;

public interface IVirtualCameraService
{
    bool IsAvailable { get; }

    bool IsActive { get; }

    string DisplayName { get; }

    event Action? StatusChanged;

    Task<bool> EnsureReadyAsync();
}
