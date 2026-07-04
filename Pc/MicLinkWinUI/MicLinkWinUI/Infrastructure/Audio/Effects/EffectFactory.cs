namespace MicLinkWinUI.Infrastructure.Audio.Effects;

using System.Buffers.Binary;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;

public static class EffectFactory
{
    public static IAudioEffect Create(EffectSlotSnapshot slot)
    {
        if (BuiltInEffectCatalog.ParseTemplateId(slot.TemplateId) is { } builtIn)
        {
            return CreateBuiltIn(slot.SlotId, builtIn, slot.Parameters);
        }

        if (slot.TemplateId.StartsWith("vst:", StringComparison.OrdinalIgnoreCase))
        {
            var path = slot.TemplateId["vst:".Length..];
            var name = Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'));
            if (name.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^5];
            }

            return new VstPassThroughEffect(slot.SlotId, name, path);
        }

        return new GainEffect(slot.SlotId, slot.Parameters);
    }

    public static IAudioEffect CreateBuiltIn(string slotId, BuiltInEffectType type, IReadOnlyDictionary<string, float>? parameters = null)
    {
        parameters ??= new Dictionary<string, float>();
        return type switch
        {
            BuiltInEffectType.Gain => new GainEffect(slotId, parameters),
            BuiltInEffectType.NoiseGate => new NoiseGateEffect(slotId, parameters),
            BuiltInEffectType.Compressor => new CompressorEffect(slotId, parameters),
            BuiltInEffectType.HighPass => new HighPassEffect(slotId, parameters),
            BuiltInEffectType.LowPass => new LowPassEffect(slotId, parameters),
            BuiltInEffectType.Equalizer => new EqualizerEffect(slotId, parameters),
            BuiltInEffectType.Limiter => new LimiterEffect(slotId, parameters),
            BuiltInEffectType.DeEsser => new DeEsserEffect(slotId, parameters),
            _ => new GainEffect(slotId, parameters),
        };
    }
}

internal abstract class EffectBase : IAudioEffect
{
    private readonly Dictionary<string, float> _parameters;

    protected EffectBase(string slotId, string displayName, IReadOnlyDictionary<string, float> defaults)
    {
        SlotId = slotId;
        DisplayName = displayName;
        _parameters = defaults.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal);
    }

    public string SlotId { get; }

    public string DisplayName { get; }

    public bool IsEnabled { get; set; } = true;

    public virtual bool IsFunctional => true;

    public IReadOnlyDictionary<string, float> Parameters => _parameters;

    public void SetParameter(string key, float value)
    {
        if (_parameters.ContainsKey(key))
        {
            _parameters[key] = value;
        }
    }

    protected float Param(string key) => _parameters.TryGetValue(key, out var value) ? value : 0f;

    public abstract void Process(Span<short> samples, int sampleRate);
}

internal sealed class GainEffect : EffectBase
{
    public GainEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "Усиление", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float> { ["gainDb"] = 0f }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var gain = MathF.Pow(10f, Param("gainDb") / 20f);
        for (var i = 0; i < samples.Length; i++)
        {
            var value = samples[i] * gain;
            samples[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }
}

internal sealed class NoiseGateEffect : EffectBase
{
    private float _envelope;

    public NoiseGateEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "Шумовой гейт", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float>
        {
            ["thresholdDb"] = -45f,
            ["attackMs"] = 5f,
            ["releaseMs"] = 80f,
        }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var threshold = MathF.Pow(10f, Param("thresholdDb") / 20f);
        var attack = MathF.Exp(-1f / (sampleRate * Param("attackMs") / 1000f));
        var release = MathF.Exp(-1f / (sampleRate * Param("releaseMs") / 1000f));

        for (var i = 0; i < samples.Length; i++)
        {
            var level = MathF.Abs(samples[i]) / short.MaxValue;
            _envelope = level >= threshold
                ? 1f - (1f - _envelope) * attack
                : _envelope * release;

            samples[i] = (short)(samples[i] * _envelope);
        }
    }
}

internal sealed class CompressorEffect : EffectBase
{
    private float _envelope;

    public CompressorEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "Компрессор", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float>
        {
            ["thresholdDb"] = -18f,
            ["ratio"] = 3f,
            ["makeupDb"] = 4f,
        }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var threshold = Param("thresholdDb");
        var ratio = MathF.Max(1f, Param("ratio"));
        var makeup = MathF.Pow(10f, Param("makeupDb") / 20f);
        var coeff = MathF.Exp(-1f / (sampleRate * 0.01f));

        for (var i = 0; i < samples.Length; i++)
        {
            var normalized = samples[i] / (float)short.MaxValue;
            var abs = MathF.Max(MathF.Abs(normalized), 1e-6f);
            var db = 20f * MathF.Log10(abs);
            _envelope = _envelope * coeff + db * (1f - coeff);

            var gainDb = _envelope <= threshold
                ? 0f
                : (threshold - _envelope) * (1f - 1f / ratio);

            var gain = MathF.Pow(10f, gainDb / 20f) * makeup;
            samples[i] = (short)Math.Clamp(normalized * gain * short.MaxValue, short.MinValue, short.MaxValue);
        }
    }
}

internal sealed class LimiterEffect : EffectBase
{
    public LimiterEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "Limiter", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float> { ["ceilingDb"] = -1f }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var ceiling = MathF.Pow(10f, Param("ceilingDb") / 20f) * short.MaxValue;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Clamp(samples[i], -ceiling, ceiling);
        }
    }
}

internal sealed class DeEsserEffect : EffectBase
{
    private float _bandState;

    public DeEsserEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "De-esser", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float>
        {
            ["frequencyHz"] = 6500f,
            ["reductionDb"] = 6f,
        }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var freq = MathF.Max(2000f, Param("frequencyHz"));
        var alpha = MathF.Exp(-2f * MathF.PI * freq / sampleRate);
        var reduction = MathF.Pow(10f, -Param("reductionDb") / 20f);

        for (var i = 0; i < samples.Length; i++)
        {
            var x = samples[i];
            _bandState = alpha * _bandState + (1f - alpha) * x;
            var sibilance = x - _bandState;
            if (MathF.Abs(sibilance) > short.MaxValue * 0.08f)
            {
                samples[i] = (short)(x - sibilance * (1f - reduction));
            }
        }
    }
}

internal sealed class HighPassEffect : EffectBase
{
    private float _prevIn;
    private float _prevOut;

    public HighPassEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "High-pass", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float> { ["cutoffHz"] = 120f }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var rc = 1f / (2f * MathF.PI * MathF.Max(20f, Param("cutoffHz")));
        var dt = 1f / sampleRate;
        var alpha = rc / (rc + dt);

        for (var i = 0; i < samples.Length; i++)
        {
            var x = samples[i];
            var y = alpha * (_prevOut + x - _prevIn);
            _prevIn = x;
            _prevOut = y;
            samples[i] = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
        }
    }
}

internal sealed class LowPassEffect : EffectBase
{
    private float _state;

    public LowPassEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "Low-pass", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float> { ["cutoffHz"] = 8000f }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var rc = 1f / (2f * MathF.PI * Math.Clamp(Param("cutoffHz"), 200f, 18000f));
        var dt = 1f / sampleRate;
        var alpha = dt / (rc + dt);

        for (var i = 0; i < samples.Length; i++)
        {
            _state += alpha * (samples[i] - _state);
            samples[i] = (short)Math.Clamp(_state, short.MinValue, short.MaxValue);
        }
    }
}

internal sealed class EqualizerEffect : EffectBase
{
    private float _lowState;
    private float _highState;

    public EqualizerEffect(string slotId, IReadOnlyDictionary<string, float> parameters)
        : base(slotId, "3-band EQ", EffectParameterHelpers.Merge(parameters, new Dictionary<string, float>
        {
            ["lowDb"] = 0f,
            ["midDb"] = 0f,
            ["highDb"] = 0f,
        }))
    {
    }

    public override void Process(Span<short> samples, int sampleRate)
    {
        var lowGain = MathF.Pow(10f, Param("lowDb") / 20f);
        var midGain = MathF.Pow(10f, Param("midDb") / 20f);
        var highGain = MathF.Pow(10f, Param("highDb") / 20f);

        var alphaLow = MathF.Exp(-2f * MathF.PI * 250f / sampleRate);
        var alphaHigh = MathF.Exp(-2f * MathF.PI * 4000f / sampleRate);

        for (var i = 0; i < samples.Length; i++)
        {
            var x = samples[i];
            _lowState = alphaLow * _lowState + (1f - alphaLow) * x;
            _highState = alphaHigh * _highState + (1f - alphaHigh) * x;

            var low = _lowState * (lowGain - 1f);
            var high = (x - _highState) * (highGain - 1f);
            var mid = (x - _lowState - (x - _highState)) * (midGain - 1f);

            var y = x + low + mid + high;
            samples[i] = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
        }
    }
}

internal sealed class VstPassThroughEffect : EffectBase
{
    public VstPassThroughEffect(string slotId, string name, string path)
        : base(slotId, name, new Dictionary<string, float>())
    {
        PluginPath = path;
    }

    public string PluginPath { get; }

    public override bool IsFunctional => false;

    public override void Process(Span<short> samples, int sampleRate)
    {
        // VST hosting is not wired yet — pass-through so chain order stays intact.
    }
}

internal static class EffectParameterHelpers
{
    internal static Dictionary<string, float> Merge(IReadOnlyDictionary<string, float> source, Dictionary<string, float> defaults)
    {
        foreach (var kv in source)
        {
            defaults[kv.Key] = kv.Value;
        }

        return defaults;
    }
}

internal static class EffectParameterDefaults
{
    public static IReadOnlyList<(string Key, string Label, float Min, float Max, float Default, string Unit)> For(
        BuiltInEffectType type) =>
        type switch
        {
            BuiltInEffectType.Gain =>
            [
                ("gainDb", "Gain", -24f, 12f, 0f, "dB"),
            ],
            BuiltInEffectType.NoiseGate =>
            [
                ("thresholdDb", "Порог", -60f, 0f, -45f, "dB"),
                ("attackMs", "Attack", 1f, 50f, 5f, "ms"),
                ("releaseMs", "Release", 10f, 500f, 80f, "ms"),
            ],
            BuiltInEffectType.Compressor =>
            [
                ("thresholdDb", "Порог", -40f, 0f, -18f, "dB"),
                ("ratio", "Ratio", 1f, 12f, 3f, ":1"),
                ("makeupDb", "Make-up", 0f, 12f, 4f, "dB"),
            ],
            BuiltInEffectType.Limiter =>
            [
                ("ceilingDb", "Потолок", -12f, 0f, -1f, "dB"),
            ],
            BuiltInEffectType.DeEsser =>
            [
                ("frequencyHz", "Частота", 3000f, 10000f, 6500f, "Hz"),
                ("reductionDb", "Подавление", 1f, 18f, 6f, "dB"),
            ],
            BuiltInEffectType.HighPass =>
            [
                ("cutoffHz", "Cutoff", 40f, 500f, 120f, "Hz"),
            ],
            BuiltInEffectType.LowPass =>
            [
                ("cutoffHz", "Cutoff", 1000f, 16000f, 8000f, "Hz"),
            ],
            BuiltInEffectType.Equalizer =>
            [
                ("lowDb", "Low", -12f, 12f, 0f, "dB"),
                ("midDb", "Mid", -12f, 12f, 0f, "dB"),
                ("highDb", "High", -12f, 12f, 0f, "dB"),
            ],
            _ => [],
        };
}
