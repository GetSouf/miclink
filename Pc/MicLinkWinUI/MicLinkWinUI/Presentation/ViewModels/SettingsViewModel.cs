namespace MicLinkWinUI.Presentation.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using Microsoft.UI.Xaml;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly ILogService _logService;
    private readonly IAudioOutputSettingsService _audioOutputSettings;
    private readonly IVirtualMicDriverService _virtualMicDriver;
    private bool _isInitializing = true;

    [ObservableProperty]
    private AppThemeMode _selectedTheme;

    [ObservableProperty]
    private string _selectedAccent = "#6C5CE7";

    [ObservableProperty]
    private bool _monitorOnSpeakers = true;

    [ObservableProperty]
    private string _driverStatusText = "Проверка…";

    [ObservableProperty]
    private bool _isDriverInstalled;

    [ObservableProperty]
    private bool _isInstallingDriver;

    public bool ShowInstallDriverButton => !IsDriverInstalled && !IsInstallingDriver;

    public Visibility InstallButtonVisibility =>
        ShowInstallDriverButton ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InstallingVisibility =>
        IsInstallingDriver ? Visibility.Visible : Visibility.Collapsed;

    public IReadOnlyList<AccentOption> AccentOptions { get; } =
    [
        new("#6C5CE7", "Violet"),
        new("#0984E3", "Ocean"),
        new("#00B894", "Mint"),
        new("#E17055", "Coral"),
        new("#FD79A8", "Rose"),
        new("#FDCB6E", "Gold"),
    ];

    public string DiscordMicHint =>
        _virtualMicDriver.CaptureDeviceName;

    public SettingsViewModel(
        IThemeService themeService,
        ILogService logService,
        IAudioOutputSettingsService audioOutputSettings,
        IVirtualMicDriverService virtualMicDriver)
    {
        _themeService = themeService;
        _logService = logService;
        _audioOutputSettings = audioOutputSettings;
        _virtualMicDriver = virtualMicDriver;

        var current = _themeService.Current;
        _selectedTheme = current.Mode;
        _selectedAccent = current.AccentColorHex;

        var audio = _audioOutputSettings.Current;
        _monitorOnSpeakers = audio.MonitorOnSpeakers;

        _virtualMicDriver.StatusChanged += OnDriverStatusChanged;
        UpdateDriverStatus();

        _isInitializing = false;
    }

    [RelayCommand]
    private void ApplyTheme()
    {
        var settings = new ThemeSettings
        {
            Mode = SelectedTheme,
            AccentColorHex = SelectedAccent
        };

        _themeService.Save(settings);
        _logService.Info($"Тема изменена: {SelectedTheme}, акцент {SelectedAccent}");
    }

    [RelayCommand]
    private async Task InstallDriverAsync()
    {
        if (IsInstallingDriver)
        {
            return;
        }

        IsInstallingDriver = true;
        try
        {
            var ok = await _virtualMicDriver.InstallDriverAsync();
            UpdateDriverStatus();
            if (ok)
            {
                _logService.Info("MicLink Microphone готов к использованию");
            }
        }
        finally
        {
            IsInstallingDriver = false;
        }
    }

    partial void OnSelectedThemeChanged(AppThemeMode value)
    {
        if (_isInitializing)
        {
            return;
        }

        ApplyTheme();
    }

    partial void OnSelectedAccentChanged(string value)
    {
        if (_isInitializing)
        {
            return;
        }

        ApplyTheme();
    }

    partial void OnMonitorOnSpeakersChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }

        SaveAudioSettings();
    }

    private void OnDriverStatusChanged() => UpdateDriverStatus();

    private void UpdateDriverStatus()
    {
        _virtualMicDriver.RefreshStatus();
        IsDriverInstalled = _virtualMicDriver.Status is not VirtualMicDriverStatus.NotInstalled;

        DriverStatusText = _virtualMicDriver.Status switch
        {
            VirtualMicDriverStatus.FeedActive => $"{_virtualMicDriver.CaptureDeviceName} — активен",
            VirtualMicDriverStatus.Installed => $"{_virtualMicDriver.CaptureDeviceName} — установлен",
            VirtualMicDriverStatus.Error => "Ошибка драйвера",
            _ => _virtualMicDriver.HasBundledDriverPackage
                ? "Нажми «Установить» (нужны права администратора)"
                : "Собери драйвер WDK → Assets/Driver (см. drivers/MicLinkVirtualAudio)"
        };

        OnPropertyChanged(nameof(ShowInstallDriverButton));
        OnPropertyChanged(nameof(InstallButtonVisibility));
        OnPropertyChanged(nameof(InstallingVisibility));
    }

    partial void OnIsInstallingDriverChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstallDriverButton));
        OnPropertyChanged(nameof(InstallButtonVisibility));
        OnPropertyChanged(nameof(InstallingVisibility));
    }

    private void SaveAudioSettings()
    {
        _audioOutputSettings.Save(new AudioOutputSettings
        {
            MonitorOnSpeakers = MonitorOnSpeakers
        });

        _logService.Info("Настройки аудиовыхода обновлены");
    }
}
