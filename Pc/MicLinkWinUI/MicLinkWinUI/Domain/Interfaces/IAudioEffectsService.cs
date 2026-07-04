namespace MicLinkWinUI.Domain.Interfaces;

using MicLinkWinUI.Domain.Models;

public interface IAudioEffectsService
{
    EffectsChainSnapshot Current { get; }

    bool HasActiveProcessors { get; }

    event Action? ChainChanged;

    void ApplyChain(EffectsChainSnapshot snapshot);

    void Process(ReadOnlySpan<byte> pcmInput, Span<byte> pcmOutput);

    IReadOnlyList<EffectLibraryEntry> GetBuiltInLibrary();

    Task<IReadOnlyList<EffectLibraryEntry>> ScanVstLibraryAsync(CancellationToken cancellationToken = default);
}
