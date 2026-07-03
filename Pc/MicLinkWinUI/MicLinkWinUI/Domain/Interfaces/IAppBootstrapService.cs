namespace MicLinkWinUI.Domain.Interfaces;

public interface IAppBootstrapService
{
    bool IsInitialized { get; }

    bool NeedsMicrophoneDriverSetup { get; }

    Task InitializeAsync();

    string MicrophoneStatus { get; }
    string CameraStatus { get; }

    event Action? StatusChanged;
}
