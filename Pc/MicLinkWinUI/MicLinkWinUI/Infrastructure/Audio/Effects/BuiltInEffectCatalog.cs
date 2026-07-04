namespace MicLinkWinUI.Infrastructure.Audio.Effects;

using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;

public static class BuiltInEffectCatalog
{
    public static IReadOnlyList<EffectLibraryEntry> Entries { get; } =
    [
        Entry(BuiltInEffectType.Gain, "Усиление", "Динамика"),
        Entry(BuiltInEffectType.NoiseGate, "Шумовой гейт", "Динамика"),
        Entry(BuiltInEffectType.Compressor, "Компрессор", "Динамика"),
        Entry(BuiltInEffectType.Limiter, "Limiter", "Динамика"),
        Entry(BuiltInEffectType.DeEsser, "De-esser", "Динамика"),
        Entry(BuiltInEffectType.HighPass, "High-pass", "Фильтры"),
        Entry(BuiltInEffectType.LowPass, "Low-pass", "Фильтры"),
        Entry(BuiltInEffectType.Equalizer, "3-band EQ", "Эквалайзер"),
    ];

    public static string TemplateId(BuiltInEffectType type) => $"builtin:{type.ToString().ToLowerInvariant()}";

    public static BuiltInEffectType? ParseTemplateId(string templateId)
    {
        if (!templateId.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = templateId["builtin:".Length..];
        return Enum.TryParse<BuiltInEffectType>(name, ignoreCase: true, out var type) ? type : null;
    }

    public static EffectLibraryEntry? Find(string templateId)
    {
        var type = ParseTemplateId(templateId);
        return type is null ? null : Entries.FirstOrDefault(e => e.BuiltInType == type);
    }

    private static EffectLibraryEntry Entry(BuiltInEffectType type, string name, string category) =>
        new()
        {
            TemplateId = TemplateId(type),
            Name = name,
            Category = category,
            SourceKind = EffectSourceKind.BuiltIn,
            BuiltInType = type,
            Detail = "Встроенный DSP",
        };
}
