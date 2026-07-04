namespace MicLinkWinUI.Presentation.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicLinkWinUI.Domain.Enums;
using MicLinkWinUI.Domain.Interfaces;
using MicLinkWinUI.Domain.Models;
using MicLinkWinUI.Infrastructure.Audio.Effects;

public partial class EffectsChainViewModel : ObservableObject
{
    private readonly IAudioEffectsService _effectsService;
    private readonly ILogService _logService;
    private bool _isLoading;
    private readonly List<EffectLibraryItemViewModel> _allVst = [];

    public EffectsChainViewModel(IAudioEffectsService effectsService, ILogService logService)
    {
        _effectsService = effectsService;
        _logService = logService;

        foreach (var entry in _effectsService.GetBuiltInLibrary())
        {
            BuiltInLibrary.Add(EffectLibraryItemViewModel.FromEntry(entry));
        }

        LoadChain(_effectsService.Current);
        _ = RefreshVstLibraryCommand.ExecuteAsync(null);
    }

    public ObservableCollection<EffectSlotViewModel> Chain { get; } = [];

    public ObservableCollection<EffectLibraryItemViewModel> BuiltInLibrary { get; } = [];

    public ObservableCollection<EffectLibraryItemViewModel> VstLibrary { get; } = [];

    [ObservableProperty]
    private EffectSlotViewModel? _selectedSlot;

    [ObservableProperty]
    private string _vstSearch = string.Empty;

    [ObservableProperty]
    private bool _isScanningVst;

    [ObservableProperty]
    private string _scanStatus = "Сканирование VST…";

    public bool HasSelectedSlot => SelectedSlot is not null;

    public bool HasParameters => SelectedSlot?.Parameters.Count > 0;

    public bool SelectedSlotIsVst => SelectedSlot?.IsVst == true;

    partial void OnSelectedSlotChanged(EffectSlotViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedSlot));
        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(SelectedSlotIsVst));
    }

    partial void OnVstSearchChanged(string value) => FilterVstLibrary();

    [RelayCommand]
    private void AddBuiltIn(EffectLibraryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        AddSlot(item.TemplateId);
    }

    [RelayCommand]
    private void AddVst(EffectLibraryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        AddSlot(item.TemplateId);
        _logService.Info($"VST добавлен в цепочку: {item.Name} (pass-through до VST host)");
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedSlot is null)
        {
            return;
        }

        Chain.Remove(SelectedSlot);
        SelectedSlot = Chain.FirstOrDefault();
        PersistChain();
    }

    [RelayCommand]
    private void MoveSelectedUp()
    {
        if (SelectedSlot is null)
        {
            return;
        }

        var index = Chain.IndexOf(SelectedSlot);
        if (index <= 0)
        {
            return;
        }

        Chain.Move(index, index - 1);
        PersistChain();
    }

    [RelayCommand]
    private void MoveSelectedDown()
    {
        if (SelectedSlot is null)
        {
            return;
        }

        var index = Chain.IndexOf(SelectedSlot);
        if (index < 0 || index >= Chain.Count - 1)
        {
            return;
        }

        Chain.Move(index, index + 1);
        PersistChain();
    }

    [RelayCommand]
    private void ApplyVoicePreset()
    {
        var snapshot = new EffectsChainSnapshot
        {
            Slots =
            [
                CreateSlot(BuiltInEffectType.HighPass, new Dictionary<string, float> { ["cutoffHz"] = 100f }),
                CreateSlot(BuiltInEffectType.NoiseGate, new Dictionary<string, float> { ["thresholdDb"] = -42f }),
                CreateSlot(BuiltInEffectType.Compressor, new Dictionary<string, float> { ["thresholdDb"] = -20f, ["ratio"] = 3f, ["makeupDb"] = 3f }),
                CreateSlot(BuiltInEffectType.DeEsser, new Dictionary<string, float> { ["reductionDb"] = 5f }),
                CreateSlot(BuiltInEffectType.Limiter, new Dictionary<string, float> { ["ceilingDb"] = -2f }),
            ],
        };

        LoadChain(snapshot);
        PersistChain();
        _logService.Info("Применён пресет «Голос для Discord»");
    }

    [RelayCommand]
    private async Task RefreshVstLibraryAsync()
    {
        if (IsScanningVst)
        {
            return;
        }

        IsScanningVst = true;
        ScanStatus = "Сканирование VST…";
        try
        {
            var entries = await _effectsService.ScanVstLibraryAsync();
            _allVst.Clear();
            foreach (var entry in entries)
            {
                _allVst.Add(EffectLibraryItemViewModel.FromEntry(entry));
            }

            FilterVstLibrary();
            ScanStatus = $"Найдено VST: {_allVst.Count}";
            _logService.Info(ScanStatus);
        }
        catch (Exception ex)
        {
            ScanStatus = "Ошибка сканирования VST";
            _logService.Warning($"VST scan failed: {ex.Message}");
        }
        finally
        {
            IsScanningVst = false;
        }
    }

    public void OnChainReordered()
    {
        if (_isLoading)
        {
            return;
        }

        PersistChain();
    }

    public void MoveSlot(EffectSlotViewModel slot, int targetIndex)
    {
        if (_isLoading)
        {
            return;
        }

        var currentIndex = Chain.IndexOf(slot);
        if (currentIndex < 0 || targetIndex < 0 || targetIndex > Chain.Count)
        {
            return;
        }

        if (currentIndex == targetIndex || currentIndex + 1 == targetIndex)
        {
            return;
        }

        Chain.RemoveAt(currentIndex);
        if (targetIndex > currentIndex)
        {
            targetIndex--;
        }

        Chain.Insert(Math.Clamp(targetIndex, 0, Chain.Count), slot);
        SelectedSlot = slot;
        PersistChain();
    }

    public void RemoveSlot(EffectSlotViewModel slot)
    {
        if (_isLoading || !Chain.Contains(slot))
        {
            return;
        }

        Chain.Remove(slot);
        SelectedSlot = Chain.FirstOrDefault();
        PersistChain();
    }

    public void OnSlotEnabledChanged(EffectSlotViewModel slot)
    {
        if (_isLoading)
        {
            return;
        }

        PersistChain();
    }

    private void AddSlot(string templateId)
    {
        var slot = CreateSlotViewModel(new EffectSlotSnapshot
        {
            SlotId = Guid.NewGuid().ToString("N"),
            TemplateId = templateId,
            IsEnabled = true,
        });

        Chain.Add(slot);
        SelectedSlot = slot;
        PersistChain();
    }

    private void LoadChain(EffectsChainSnapshot snapshot)
    {
        _isLoading = true;
        try
        {
            Chain.Clear();
            foreach (var slot in snapshot.Slots)
            {
                Chain.Add(CreateSlotViewModel(slot));
            }

            SelectedSlot = Chain.FirstOrDefault();
        }
        finally
        {
            _isLoading = false;
        }
    }

    internal void PersistChain()
    {
        var snapshot = new EffectsChainSnapshot
        {
            Slots = Chain.Select(static slot => slot.ToSnapshot()).ToList(),
        };

        _effectsService.ApplyChain(snapshot);
    }

    private EffectSlotViewModel CreateSlotViewModel(EffectSlotSnapshot snapshot)
    {
        var entry = BuiltInEffectCatalog.Find(snapshot.TemplateId);
        if (entry is null && snapshot.TemplateId.StartsWith("vst:", StringComparison.OrdinalIgnoreCase))
        {
            var path = snapshot.TemplateId["vst:".Length..];
            entry = new EffectLibraryEntry
            {
                TemplateId = snapshot.TemplateId,
                Name = Path.GetFileNameWithoutExtension(path),
                Category = "VST",
                SourceKind = EffectSourceKind.Vst,
                Detail = path,
            };
        }

        entry ??= BuiltInEffectCatalog.Entries[0];
        return new EffectSlotViewModel(entry, snapshot, this);
    }

    private static EffectSlotSnapshot CreateSlot(
        BuiltInEffectType type,
        Dictionary<string, float>? parameters = null) =>
        new()
        {
            SlotId = Guid.NewGuid().ToString("N"),
            TemplateId = BuiltInEffectCatalog.TemplateId(type),
            IsEnabled = true,
            Parameters = parameters ?? new Dictionary<string, float>(),
        };

    private void FilterVstLibrary()
    {
        VstLibrary.Clear();
        var query = VstSearch.Trim();
        foreach (var item in _allVst)
        {
            if (string.IsNullOrEmpty(query) ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Detail.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                VstLibrary.Add(item);
            }
        }
    }
}

public partial class EffectLibraryItemViewModel : ObservableObject
{
    public required string TemplateId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Detail { get; init; }
    public required bool IsVst { get; init; }

    public static EffectLibraryItemViewModel FromEntry(EffectLibraryEntry entry) =>
        new()
        {
            TemplateId = entry.TemplateId,
            Name = entry.Name,
            Category = entry.Category,
            Detail = entry.Detail ?? entry.Category,
            IsVst = entry.SourceKind == EffectSourceKind.Vst,
        };
}

public partial class EffectSlotViewModel : ObservableObject
{
    private readonly EffectsChainViewModel _owner;
    private readonly Dictionary<string, float> _parameters;

    public EffectSlotViewModel(EffectLibraryEntry entry, EffectSlotSnapshot snapshot, EffectsChainViewModel owner)
    {
        _owner = owner;
        SlotId = snapshot.SlotId;
        TemplateId = snapshot.TemplateId;
        Name = entry.Name;
        Category = entry.Category;
        IsVst = entry.SourceKind == EffectSourceKind.Vst;
        IsFunctional = entry.SourceKind == EffectSourceKind.BuiltIn;
        _isEnabled = snapshot.IsEnabled;

        _parameters = snapshot.Parameters.ToDictionary(static kv => kv.Key, static kv => kv.Value);
        Parameters = [];

        if (entry.BuiltInType is { } builtIn)
        {
            foreach (var def in EffectParameterDefaults.For(builtIn))
            {
                var value = _parameters.TryGetValue(def.Key, out var stored) ? stored : def.Default;
                _parameters[def.Key] = value;
                Parameters.Add(new EffectParameterViewModel(def.Key, def.Label, def.Min, def.Max, value, def.Unit, this));
            }
        }
    }

    public string SlotId { get; }
    public string TemplateId { get; }
    public string Name { get; }
    public string Category { get; }
    public bool IsVst { get; }
    public bool IsFunctional { get; }

    public string StatusLabel => IsVst
        ? (IsFunctional ? "VST" : "VST · pass-through")
        : "DSP";

    [ObservableProperty]
    private bool _isEnabled;

    public ObservableCollection<EffectParameterViewModel> Parameters { get; }

    partial void OnIsEnabledChanged(bool value) => _owner.OnSlotEnabledChanged(this);

    public void NotifyPersist() => _owner.PersistChain();

    public void SetParameter(string key, float value)
    {
        if (_parameters.ContainsKey(key))
        {
            _parameters[key] = value;
        }
    }

    public EffectSlotSnapshot ToSnapshot() =>
        new()
        {
            SlotId = SlotId,
            TemplateId = TemplateId,
            IsEnabled = IsEnabled,
            Parameters = _parameters.ToDictionary(static kv => kv.Key, static kv => kv.Value),
        };
}

public partial class EffectParameterViewModel : ObservableObject
{
    private readonly EffectSlotViewModel _slot;

    public EffectParameterViewModel(
        string key,
        string label,
        float min,
        float max,
        float value,
        string unit,
        EffectSlotViewModel slot)
    {
        Key = key;
        Label = label;
        Min = min;
        Max = max;
        Unit = unit;
        _slot = slot;
        _value = value;
    }

    public string Key { get; }
    public string Label { get; }
    public float Min { get; }
    public float Max { get; }
    public string Unit { get; }

    [ObservableProperty]
    private float _value;

    public string ValueDisplay => FormatValue(Value);

    partial void OnValueChanged(float value)
    {
        OnPropertyChanged(nameof(ValueDisplay));
        _slot.SetParameter(Key, value);
        _slot.NotifyPersist();
    }

    private string FormatValue(float value) =>
        Math.Abs(value - MathF.Round(value)) < 0.05f
            ? MathF.Round(value).ToString("0")
            : value.ToString("0.0");
}
