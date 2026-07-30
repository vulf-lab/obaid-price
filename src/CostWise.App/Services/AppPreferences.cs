using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CostWise.App.Services;

public sealed class AppPreferences : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private int _costDecimalPlaces = 2;

    public static AppPreferences Current { get; private set; } = null!;

    public int CostDecimalPlaces
    {
        get => _costDecimalPlaces;
        set
        {
            var clamped = Math.Clamp(value, 0, 4);
            if (_costDecimalPlaces == clamped) return;
            _costDecimalPlaces = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MoneyFormat));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Standard numeric format, e.g. N2.</summary>
    public string MoneyFormat => $"N{CostDecimalPlaces}";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public static AppPreferences CreateAndLoad()
    {
        var prefs = new AppPreferences();
        prefs.Load();
        Current = prefs;
        return prefs;
    }

    public string FormatMoney(decimal value) =>
        value.ToString(MoneyFormat, CultureInfo.CurrentCulture);

    public decimal RoundMoney(decimal value) =>
        Math.Round(value, CostDecimalPlaces, MidpointRounding.AwayFromZero);

    public void Save()
    {
        var path = FilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dto = new PrefsDto { CostDecimalPlaces = CostDecimalPlaces };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    public void Load()
    {
        var path = FilePath();
        try
        {
            if (!File.Exists(path)) return;
            var dto = JsonSerializer.Deserialize<PrefsDto>(File.ReadAllText(path));
            if (dto is null) return;
            _costDecimalPlaces = Math.Clamp(dto.CostDecimalPlaces, 0, 4);
        }
        catch
        {
            // keep defaults
        }
    }

    private static string FilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "app-preferences.json");

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class PrefsDto
    {
        public int CostDecimalPlaces { get; set; } = 2;
    }
}
