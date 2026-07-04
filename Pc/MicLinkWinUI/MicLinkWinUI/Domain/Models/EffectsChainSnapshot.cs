namespace MicLinkWinUI.Domain.Models;

using MicLinkWinUI.Domain.Enums;

public sealed class EffectsChainSnapshot
{
    public List<EffectSlotSnapshot> Slots { get; init; } = [];
}

public sealed class EffectSlotSnapshot
{
    public required string SlotId { get; init; }
    public required string TemplateId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public Dictionary<string, float> Parameters { get; init; } = new(StringComparer.Ordinal);
}

public sealed class EffectLibraryEntry
{
    public required string TemplateId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required EffectSourceKind SourceKind { get; init; }
    public string? Detail { get; init; }
    public BuiltInEffectType? BuiltInType { get; init; }
}

public sealed class VstPluginInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string Format { get; init; }
}
