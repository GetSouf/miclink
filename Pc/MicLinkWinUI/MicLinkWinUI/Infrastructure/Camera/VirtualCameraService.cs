namespace MicLinkWinUI.Infrastructure.Camera;

using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Interfaces;

/// <summary>
/// Win11 virtual camera via MFCreateVirtualCamera.
/// Full media-source COM module ships separately (MicLink.CameraSource).
/// </summary>
public sealed class VirtualCameraService : IVirtualCameraService
{
    private readonly ILogService _logService;

    public VirtualCameraService(ILogService logService)
    {
        _logService = logService;
    }

    public bool IsAvailable =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public bool IsActive { get; private set; }

    public string DisplayName => AppConstants.VirtualCameraName;

    public event Action? StatusChanged;

    public Task<bool> EnsureReadyAsync()
    {
        if (!IsAvailable)
        {
            _logService.Warning("MicLink Camera требует Windows 11 (22000+).");
            IsActive = false;
            StatusChanged?.Invoke();
            return Task.FromResult(false);
        }

        var sourcePath = ResolveCameraSourcePath();
        if (sourcePath is null)
        {
            _logService.Warning(
                "Компонент MicLink.CameraSource не найден. Камера появится после сборки media-source (Win11).");
            IsActive = false;
            StatusChanged?.Invoke();
            return Task.FromResult(false);
        }

        // Registration + MFCreateVirtualCamera will be wired when CameraSource project is added.
        _logService.Info($"Найден компонент камеры: {sourcePath}");
        IsActive = false;
        StatusChanged?.Invoke();
        return Task.FromResult(false);
    }

    private static string? ResolveCameraSourcePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "MicLink.CameraSource.dll"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "Camera", "MicLink.CameraSource.dll"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
