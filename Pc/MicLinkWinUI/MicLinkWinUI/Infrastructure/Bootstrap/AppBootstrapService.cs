namespace MicLinkWinUI.Infrastructure.Bootstrap;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;

public sealed class AppBootstrapService : IAppBootstrapService
{
    private readonly ILogService _logService;
    private readonly IVirtualMicDriverService _virtualMic;
    private readonly IVirtualCameraService _virtualCamera;

    public AppBootstrapService(
        ILogService logService,
        IVirtualMicDriverService virtualMic,
        IVirtualCameraService virtualCamera)
    {
        _logService = logService;
        _virtualMic = virtualMic;
        _virtualCamera = virtualCamera;

        _virtualMic.StatusChanged += OnDeviceStatusChanged;
        _virtualCamera.StatusChanged += OnDeviceStatusChanged;
    }

    public bool IsInitialized { get; private set; }

    public bool NeedsMicrophoneDriverSetup =>
        IsInitialized && !_virtualMic.IsCaptureDevicePresent() && _virtualMic.HasBundledDriverPackage;

    public string MicrophoneStatus { get; private set; } = "Проверка…";

    public string CameraStatus { get; private set; } = "Проверка…";

    public event Action? StatusChanged;

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        _logService.Info("Инициализация виртуальных устройств MicLink…");

        var micReady = await _virtualMic.EnsureReadyAsync();
        var cameraReady = await _virtualCamera.EnsureReadyAsync();

        UpdateStatus(micReady, cameraReady);
        IsInitialized = true;
        StatusChanged?.Invoke();

        if (micReady)
        {
            _logService.Info($"Микрофон готов: {_virtualMic.CaptureDeviceName}");
        }
        else if (!_virtualMic.HasBundledDriverPackage)
        {
            _logService.Warning(
                "Драйвер MicLink Microphone не собран. См. drivers/MicLinkVirtualAudio/README.md (нужен WDK, один раз).");
        }

        if (cameraReady)
        {
            _logService.Info($"Камера готова: {_virtualCamera.DisplayName}");
        }
    }

    private void OnDeviceStatusChanged() =>
        UpdateStatus(_virtualMic.IsCaptureDevicePresent(), _virtualCamera.IsActive);

    private void UpdateStatus(bool micReady, bool cameraReady)
    {
        MicrophoneStatus = micReady
            ? $"{_virtualMic.CaptureDeviceName} — готов"
            : _virtualMic.HasBundledDriverPackage
                ? $"{AppConstants.VirtualMicName} — установка не завершена"
                : $"{AppConstants.VirtualMicName} — нужна сборка драйвера (WDK)";

        CameraStatus = cameraReady
            ? $"{_virtualCamera.DisplayName} — готова"
            : OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
                ? $"{AppConstants.VirtualCameraName} — подключается…"
                : $"{AppConstants.VirtualCameraName} — нужен Windows 11";

        StatusChanged?.Invoke();
    }
}
