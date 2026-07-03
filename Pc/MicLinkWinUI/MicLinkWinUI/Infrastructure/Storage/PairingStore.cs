namespace MicLinkWinUI.Infrastructure.Storage;

using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using System.Text.Json;

public sealed class PairingStore : IPairingStore
{
    private const string StorageKey = "paired_device";

    private readonly LocalSettingsStore _store;

    public PairingStore(LocalSettingsStore store)
    {
        _store = store;
    }

    public PairedDeviceInfo? Load()
    {
        var json = _store.Get(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PairedDeviceInfo>(json);
    }

    public void Save(PairedDeviceInfo device)
    {
        var json = JsonSerializer.Serialize(device);
        _store.Set(StorageKey, json);
    }

    public void Clear() => _store.Set(StorageKey, string.Empty);
}
