namespace MicLinkWinUI.Infrastructure.Network;

using Makaretu.Dns;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Infrastructure.Network.Protocol;

public sealed class MdnsHostAdvertiser : IAsyncDisposable
{
    private MulticastService? _mdns;
    private ServiceDiscovery? _discovery;

    public Task StartAsync(string pcName, CancellationToken cancellationToken = default)
    {
        _mdns = new MulticastService();
        _mdns.Start();
        _discovery = new ServiceDiscovery(_mdns);

        var profile = new ServiceProfile(
            AppConstants.AppName,
            "_miclink._tcp",
            (ushort)AppConstants.DefaultPort);

        profile.AddProperty("pcname", pcName);
        profile.AddProperty("version", MicLinkProtocol.Version.ToString());

        _discovery.Advertise(profile);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _discovery?.Dispose();
        _mdns?.Dispose();
        return ValueTask.CompletedTask;
    }
}
