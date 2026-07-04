namespace MicLinkWinUI.Core.DependencyInjection;

using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Infrastructure.Audio;
using MicLinkWinUI.Infrastructure.Bootstrap;
using MicLinkWinUI.Infrastructure.Camera;
using MicLinkWinUI.Infrastructure.Logging;
using MicLinkWinUI.Infrastructure.Network;
using MicLinkWinUI.Infrastructure.Storage;
using MicLinkWinUI.Infrastructure.Theming;
using MicLinkWinUI.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMicLinkServices(this IServiceCollection services)
    {
        services.AddSingleton<LocalSettingsStore>();
        services.AddSingleton<IPairingStore, PairingStore>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IVirtualMicDriverService, VirtualMicDriverService>();
        services.AddSingleton<IVirtualCameraService, VirtualCameraService>();
        services.AddSingleton<IAppBootstrapService, AppBootstrapService>();
        services.AddSingleton<IAudioOutputSettingsService, AudioOutputSettingsService>();
        services.AddSingleton<IAudioEffectsService, AudioEffectsService>();
        services.AddSingleton<IAudioPlaybackService, PcmPlaybackService>();
        services.AddSingleton<AudioTcpServer>();
        services.AddSingleton<IConnectionHostService, ConnectionHostService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<EffectsChainViewModel>();
        services.AddSingleton<LogsViewModel>();

        return services;
    }
}
