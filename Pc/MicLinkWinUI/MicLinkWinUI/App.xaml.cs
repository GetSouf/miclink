using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Core.DependencyInjection;
using MicLinkWinUI.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace MicLinkWinUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ConfigureServices();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();

        await AppServices.GetRequired<IAppBootstrapService>().InitializeAsync();
        await window.PromptDriverSetupIfNeededAsync();
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddMicLinkServices();
        AppServices.Configure(services);
    }
}
