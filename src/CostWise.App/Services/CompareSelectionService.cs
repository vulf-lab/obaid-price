using System.IO;
using System.Text.Json;

namespace CostWise.App.Services;

/// <summary>Shared selection bag for profile/formulation comparison (max 10 each). Persists until Clear.</summary>
public sealed class CompareSelectionService
{
    public const int MaxItems = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly List<string> _profileCodes = new();
    private readonly List<FormulationCompareRef> _formulations = new();
    private bool _suppressPersist;

    public event EventHandler? Changed;

    public IReadOnlyList<string> ProfileCodes => _profileCodes;
    public IReadOnlyList<FormulationCompareRef> Formulations => _formulations;

    public int ProfileCount => _profileCodes.Count;
    public int FormulationCount => _formulations.Count;

    public bool CanCompareProfiles => _profileCodes.Count >= 2;
    public bool CanCompareFormulations => _formulations.Count >= 2;

    public CompareSelectionService()
    {
        Load();
    }

    public bool IsProfileSelected(string code) =>
        _profileCodes.Contains(code, StringComparer.OrdinalIgnoreCase);

    public bool IsFormulationSelected(int id) =>
        _formulations.Any(f => f.Id == id);

    public bool TrySetProfile(string code, bool selected, out string? error)
    {
        error = null;
        code = code.Trim();
        if (string.IsNullOrEmpty(code)) return false;

        var idx = _profileCodes.FindIndex(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
        if (selected)
        {
            if (idx >= 0) return true;
            if (_profileCodes.Count >= MaxItems)
            {
                error = $"Compare is limited to {MaxItems} profiles.";
                return false;
            }
            _profileCodes.Add(code);
        }
        else if (idx >= 0)
        {
            _profileCodes.RemoveAt(idx);
        }
        else
        {
            return true;
        }

        NotifyChanged();
        return true;
    }

    public bool TrySetFormulation(int id, string code, bool selected, out string? error)
    {
        error = null;
        var idx = _formulations.FindIndex(f => f.Id == id);
        if (selected)
        {
            if (idx >= 0) return true;
            if (_formulations.Count >= MaxItems)
            {
                error = $"Compare is limited to {MaxItems} formulations.";
                return false;
            }
            _formulations.Add(new FormulationCompareRef(id, code));
        }
        else if (idx >= 0)
        {
            _formulations.RemoveAt(idx);
        }
        else
        {
            return true;
        }

        NotifyChanged();
        return true;
    }

    public void ClearProfiles()
    {
        if (_profileCodes.Count == 0) return;
        _profileCodes.Clear();
        NotifyChanged();
    }

    public void ClearFormulations()
    {
        if (_formulations.Count == 0) return;
        _formulations.Clear();
        NotifyChanged();
    }

    public void RemoveProfile(string code)
    {
        TrySetProfile(code, false, out _);
    }

    public void RemoveFormulation(int id)
    {
        var existing = _formulations.FirstOrDefault(f => f.Id == id);
        if (existing is null) return;
        TrySetFormulation(id, existing.Code, false, out _);
    }

    /// <summary>Reorder profile codes (e.g. after column sort). Same set required. Persists without reload.</summary>
    public void ReorderProfiles(IReadOnlyList<string> orderedCodes)
    {
        if (orderedCodes.Count == 0) return;
        var next = new List<string>();
        foreach (var code in orderedCodes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            var trimmed = code.Trim();
            if (!_profileCodes.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) continue;
            if (next.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) continue;
            next.Add(trimmed);
        }

        // Keep any codes missing from ordered list at the end (should not happen).
        foreach (var code in _profileCodes)
        {
            if (!next.Contains(code, StringComparer.OrdinalIgnoreCase))
                next.Add(code);
        }

        if (next.SequenceEqual(_profileCodes, StringComparer.OrdinalIgnoreCase)) 
        {
            Save();
            return;
        }

        _profileCodes.Clear();
        _profileCodes.AddRange(next);
        Save();
    }

    /// <summary>Reorder formulations (e.g. after column sort). Persists without reload.</summary>
    public void ReorderFormulations(IReadOnlyList<FormulationCompareRef> ordered)
    {
        if (ordered.Count == 0) return;
        var byId = _formulations.ToDictionary(f => f.Id);
        var next = new List<FormulationCompareRef>();
        foreach (var item in ordered)
        {
            if (!byId.ContainsKey(item.Id)) continue;
            if (next.Any(f => f.Id == item.Id)) continue;
            next.Add(item);
        }

        foreach (var f in _formulations)
        {
            if (next.All(x => x.Id != f.Id))
                next.Add(f);
        }

        if (next.Select(f => f.Id).SequenceEqual(_formulations.Select(f => f.Id)))
        {
            Save();
            return;
        }

        _formulations.Clear();
        _formulations.AddRange(next);
        Save();
    }

    private void NotifyChanged()
    {
        if (!_suppressPersist)
            Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "compare-selection.json");

    private void Load()
    {
        _suppressPersist = true;
        try
        {
            if (!File.Exists(FilePath)) return;
            var dto = JsonSerializer.Deserialize<CompareSelectionDto>(File.ReadAllText(FilePath), JsonOptions);
            if (dto is null) return;

            _profileCodes.Clear();
            foreach (var code in dto.ProfileCodes ?? [])
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (_profileCodes.Count >= MaxItems) break;
                if (_profileCodes.Contains(code, StringComparer.OrdinalIgnoreCase)) continue;
                _profileCodes.Add(code.Trim());
            }

            _formulations.Clear();
            foreach (var f in dto.Formulations ?? [])
            {
                if (f is null || f.Id <= 0 || string.IsNullOrWhiteSpace(f.Code)) continue;
                if (_formulations.Count >= MaxItems) break;
                if (_formulations.Any(x => x.Id == f.Id)) continue;
                _formulations.Add(new FormulationCompareRef(f.Id, f.Code.Trim()));
            }
        }
        catch
        {
            // keep empty defaults
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var dto = new CompareSelectionDto
            {
                ProfileCodes = _profileCodes.ToList(),
                Formulations = _formulations
                    .Select(f => new FormulationDto { Id = f.Id, Code = f.Code })
                    .ToList()
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // ignore disk errors
        }
    }

    private sealed class CompareSelectionDto
    {
        public List<string>? ProfileCodes { get; set; }
        public List<FormulationDto>? Formulations { get; set; }
    }

    private sealed class FormulationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}

public sealed record FormulationCompareRef(int Id, string Code);
