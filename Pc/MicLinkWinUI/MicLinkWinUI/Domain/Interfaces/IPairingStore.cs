namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Models;

public interface IPairingStore
{
    PairedDeviceInfo? Load();
    void Save(PairedDeviceInfo device);
    void Clear();
}
