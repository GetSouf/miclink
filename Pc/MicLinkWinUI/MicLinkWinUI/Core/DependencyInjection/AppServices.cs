namespace MicLinkWinUI.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

public static class AppServices
{
    public static IServiceProvider Provider { get; private set; } = null!;

    public static void Configure(IServiceCollection services)
    {
        Provider = services.BuildServiceProvider();
    }

    public static T GetRequired<T>() where T : notnull
        => Provider.GetRequiredService<T>();
}
