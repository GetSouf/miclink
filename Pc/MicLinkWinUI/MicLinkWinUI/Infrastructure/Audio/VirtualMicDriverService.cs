namespace MicLinkWinUI.Infrastructure.Audio;

using System.Diagnostics;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using NAudio.CoreAudioApi;

public sealed class VirtualMicDriverService : IVirtualMicDriverService, IDisposable
{
    private readonly ILogService _logService;
    private readonly object _gate = new();
    private Microsoft.Win32.SafeHandles.SafeFileHandle? _feedHandle;
    private VirtualMicDriverStatus _status = VirtualMicDriverStatus.NotInstalled;
    private string _captureDeviceName = AppConstants.VirtualMicName;

    public VirtualMicDriverService(ILogService logService)
    {
        _logService = logService;
        RefreshStatus();
    }

    public VirtualMicDriverStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public string CaptureDeviceName => _captureDeviceName;

    public bool HasBundledDriverPackage => TryResolveDriverPackage(out _, out _);

    public event Action? StatusChanged;

    public void RefreshStatus()
    {
        _captureDeviceName = FindCaptureDeviceName() ?? AppConstants.VirtualMicName;
        var next = ResolveStatus();
        lock (_gate)
        {
            if (_status == next)
            {
                return;
            }

            _status = next;
        }

        StatusChanged?.Invoke();
    }

    public bool IsCaptureDevicePresent() =>
        FindCaptureDeviceName() is not null || CanOpenControlDevice();

    public async Task<bool> EnsureReadyAsync()
    {
        RefreshStatus();
        if (IsCaptureDevicePresent())
        {
            return true;
        }

        if (!HasBundledDriverPackage)
        {
            return false;
        }

        _logService.Info("Установка драйвера MicLink Microphone…");
        return await InstallDriverAsync();
    }

    public bool TryOpenFeed()
    {
        lock (_gate)
        {
            CloseFeedInternal();

            var handle = MicLinkNative.CreateFile(
                MicLinkNative.UserDeviceName,
                MicLinkNative.GenericRead | MicLinkNative.GenericWrite,
                MicLinkNative.FileShareRead | MicLinkNative.FileShareWrite,
                nint.Zero,
                MicLinkNative.OpenExisting,
                MicLinkNative.FileAttributeNormal,
                nint.Zero);

            if (handle.IsInvalid)
            {
                _logService.Warning(
                    $"{AppConstants.VirtualMicName} не отвечает. Проверь установку драйвера в настройках.");
                RefreshStatus();
                return false;
            }

            _feedHandle = handle;
            _status = VirtualMicDriverStatus.FeedActive;
            StatusChanged?.Invoke();
            _logService.Info($"{AppConstants.VirtualMicName} — канал открыт");
            return true;
        }
    }

    public void CloseFeed()
    {
        lock (_gate)
        {
            CloseFeedInternal();
            RefreshStatus();
        }
    }

    public bool TryWritePcm(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty)
        {
            return true;
        }

        Microsoft.Win32.SafeHandles.SafeFileHandle? handle;
        lock (_gate)
        {
            handle = _feedHandle;
        }

        if (handle is null || handle.IsInvalid)
        {
            return false;
        }

        var buffer = pcm.ToArray();
        var ok = MicLinkNative.DeviceIoControl(
            handle,
            MicLinkNative.IoctlWritePcm,
            buffer,
            buffer.Length,
            nint.Zero,
            0,
            out _,
            nint.Zero);

        if (!ok)
        {
            _logService.Warning($"Ошибка записи в {AppConstants.VirtualMicName}");
            return false;
        }

        return true;
    }

    public async Task<bool> InstallDriverAsync()
    {
        if (!TryResolveDriverPackage(out var infPath, out _))
        {
            _logService.Error(
                "Пакет драйвера не найден. Собери MicLinkVirtualAudio.sys (WDK) и положи в Assets/Driver.");
            return false;
        }

        try
        {
            RefreshStatus();
            if (IsCaptureDevicePresent())
            {
                _logService.Info($"{AppConstants.VirtualMicName} уже установлен ({CaptureDeviceName})");
                return true;
            }

            var bundledScript = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "install-driver.ps1");
            if (File.Exists(bundledScript))
            {
                _logService.Info("Установка драйвера MicLink Microphone…");
                var scriptOk = await RunElevatedAsync(
                    "powershell.exe",
                    $"-ExecutionPolicy Bypass -NoProfile -File \"{bundledScript}\"",
                    "Установка драйвера отменена");

                RefreshStatus();
                if (IsCaptureDevicePresent())
                {
                    _logService.Info($"{AppConstants.VirtualMicName} установлен");
                    return true;
                }

                if (!scriptOk)
                {
                    return false;
                }
            }

            var devgenPath = ResolveDevGenPath();
            if (devgenPath is not null)
            {
                _logService.Info("Создание виртуального аудиоустройства…");
                await RunElevatedAsync(
                    devgenPath,
                    $"/add /bus ROOT /hardwareid {AppConstants.VirtualAudioHardwareId}",
                    "Создание устройства отменено");

                RefreshStatus();
                if (IsCaptureDevicePresent())
                {
                    _logService.Info($"{AppConstants.VirtualMicName} установлен");
                    return true;
                }
            }
            else
            {
                _logService.Warning("devgen.exe не найден (нужен WDK). Пробуем только pnputil…");
            }

            var pnputilOk = await RunElevatedAsync(
                "pnputil",
                $"/add-driver \"{infPath}\" /install",
                "Установка драйвера отменена");

            RefreshStatus();
            if (IsCaptureDevicePresent())
            {
                _logService.Info($"{AppConstants.VirtualMicName} установлен");
                return true;
            }

            if (!pnputilOk)
            {
                return false;
            }

            _logService.Warning(
                "Драйвер в хранилище Windows, но endpoint не найден. " +
                "Admin: drivers\\scripts\\install-driver-device.ps1");
            return false;
        }
        catch (Exception ex)
        {
            _logService.Error($"Не удалось установить драйвер: {ex.Message}");
            return false;
        }
    }

    private static string? ResolveDevGenPath()
    {
        var kitsTools = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits", "10", "Tools");

        if (!Directory.Exists(kitsTools))
        {
            return null;
        }

        var candidates = Directory.EnumerateFiles(kitsTools, "devgen.exe", SearchOption.AllDirectories)
            .Where(p => p.Contains(@"\x64\", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase);

        return candidates.FirstOrDefault();
    }

    private async Task<bool> RunElevatedAsync(string fileName, string arguments, string cancelMessage)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            _logService.Warning(cancelMessage);
            return false;
        }

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var hex = process.ExitCode & 0xFFFFFFFF;
            if (hex == 0xE000022F)
            {
                _logService.Error(
                    "Драйвер не подписан (0xE000022F). Windows блокирует установку без test signing. " +
                    "1) PowerShell от администратора: bcdedit /set testsigning on " +
                    "2) Перезагрузка ПК (в углу появится Test Mode) " +
                    "3) Снова «Установить драйвер» в MicLink. " +
                    "Скрипт: drivers\\scripts\\enable-test-signing.ps1");
            }
            else
            {
                _logService.Error($"{fileName} завершился с кодом {process.ExitCode} (0x{hex:X}). " +
                    "Нужны: test signing + перезагрузка, запуск от администратора.");
            }

            return false;
        }

        return true;
    }

    public void Dispose()
    {
        CloseFeed();
    }

    private VirtualMicDriverStatus ResolveStatus()
    {
        lock (_gate)
        {
            if (_feedHandle is not null && !_feedHandle.IsInvalid)
            {
                return VirtualMicDriverStatus.FeedActive;
            }
        }

        if (IsCaptureDevicePresent())
        {
            return VirtualMicDriverStatus.Installed;
        }

        return VirtualMicDriverStatus.NotInstalled;
    }

    private void CloseFeedInternal()
    {
        _feedHandle?.Dispose();
        _feedHandle = null;
    }

    private static string? FindCaptureDeviceName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var states = DeviceState.Active | DeviceState.Disabled | DeviceState.Unplugged;
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, states);

            foreach (var device in devices)
            {
                if (IsMicLinkCaptureEndpoint(device.FriendlyName))
                {
                    return device.FriendlyName;
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static bool CanOpenControlDevice()
    {
        try
        {
            using var handle = MicLinkNative.CreateFile(
                MicLinkNative.UserDeviceName,
                MicLinkNative.GenericRead,
                MicLinkNative.FileShareRead | MicLinkNative.FileShareWrite,
                nint.Zero,
                MicLinkNative.OpenExisting,
                MicLinkNative.FileAttributeNormal,
                nint.Zero);

            return !handle.IsInvalid;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMicLinkCaptureEndpoint(string name) =>
        name.Contains("MicLink", StringComparison.OrdinalIgnoreCase) &&
        (name.Contains("Microphone", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Virtual Audio", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Микрофон", StringComparison.OrdinalIgnoreCase));

    private static bool TryResolveDriverPackage(out string infPath, out string sysPath)
    {
        var folder = ResolveDriverFolder();
        infPath = Path.Combine(folder, "MicLinkVirtualAudio.inf");
        sysPath = Path.Combine(folder, "MicLinkVirtualAudio.sys");

        if (File.Exists(infPath) && File.Exists(sysPath))
        {
            return true;
        }

        infPath = string.Empty;
        sysPath = string.Empty;
        return false;
    }

    private static string ResolveDriverFolder()
    {
        var fromApp = Path.Combine(AppContext.BaseDirectory, "Assets", "Driver");
        if (Directory.Exists(fromApp))
        {
            return fromApp;
        }

        var fromRepo = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "drivers", "MicLinkVirtualAudio", "install"));

        return Directory.Exists(fromRepo) ? fromRepo : fromApp;
    }
}
