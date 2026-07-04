namespace MicLinkWinUI.Infrastructure.Audio;

using System.Runtime.InteropServices;
using System.Text.Json;
using MicLinkWinUI.Core.Constants;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using MicLinkWinUI.Infrastructure.Audio.Effects;
using MicLinkWinUI.Infrastructure.Storage;

public sealed class AudioEffectsService : IAudioEffectsService
{
    private readonly LocalSettingsStore _store;
    private readonly object _gate = new();
    private readonly List<IAudioEffect> _processors = [];
    private EffectsChainSnapshot _current = new();

    public AudioEffectsService(LocalSettingsStore store)
    {
        _store = store;
        _current = Load();
        RebuildProcessors(_current);
    }

    public EffectsChainSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool HasActiveProcessors
    {
        get
        {
            lock (_gate)
            {
                return _processors.Any(static p => p.IsEnabled && p.IsFunctional);
            }
        }
    }

    public event Action? ChainChanged;

    public void ApplyChain(EffectsChainSnapshot snapshot)
    {
        lock (_gate)
        {
            _current = snapshot;
            RebuildProcessors(snapshot);
            Save(snapshot);
        }

        ChainChanged?.Invoke();
    }

    public void Process(ReadOnlySpan<byte> pcmInput, Span<byte> pcmOutput)
    {
        if (pcmInput.Length > pcmOutput.Length)
        {
            throw new ArgumentException("Output buffer is too small.");
        }

        pcmInput.CopyTo(pcmOutput);

        List<IAudioEffect> processors;
        lock (_gate)
        {
            processors = _processors.ToList();
        }

        if (processors.Count == 0)
        {
            return;
        }

        var sampleCount = pcmOutput.Length / 2;
        var samples = MemoryMarshal.Cast<byte, short>(pcmOutput[..(sampleCount * 2)]);

        foreach (var processor in processors)
        {
            if (!processor.IsEnabled || !processor.IsFunctional)
            {
                continue;
            }

            processor.Process(samples, AudioConstants.SampleRate);
        }
    }

    public IReadOnlyList<EffectLibraryEntry> GetBuiltInLibrary() => BuiltInEffectCatalog.Entries;

    public async Task<IReadOnlyList<EffectLibraryEntry>> ScanVstLibraryAsync(CancellationToken cancellationToken = default)
    {
        var plugins = await VstPluginScanner.ScanAsync(cancellationToken);
        return plugins.Select(static plugin => new EffectLibraryEntry
        {
            TemplateId = $"vst:{plugin.Path}",
            Name = plugin.Name,
            Category = plugin.Format,
            SourceKind = EffectSourceKind.Vst,
            Detail = plugin.Path,
        }).ToList();
    }

    private void RebuildProcessors(EffectsChainSnapshot snapshot)
    {
        _processors.Clear();
        foreach (var slot in snapshot.Slots)
        {
            var processor = EffectFactory.Create(slot);
            processor.IsEnabled = slot.IsEnabled;
            _processors.Add(processor);
        }
    }

    private EffectsChainSnapshot Load()
    {
        var json = _store.Get(AppConstants.SettingsKeyEffectsChain);
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefaultChain();
        }

        try
        {
            return JsonSerializer.Deserialize<EffectsChainSnapshot>(json) ?? CreateDefaultChain();
        }
        catch
        {
            return CreateDefaultChain();
        }
    }

    private void Save(EffectsChainSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        _store.Set(AppConstants.SettingsKeyEffectsChain, json);
    }

    private static EffectsChainSnapshot CreateDefaultChain() => new();
}
