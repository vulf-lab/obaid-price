using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CostWise.App.Services.Update;

namespace CostWise.App.Services;

public sealed class AppPreferences : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private int _costDecimalPlaces = 2;
    private int _kesDecimalPlaces = 2;
    private int _usdDecimalPlaces = 2;
    private decimal _exchangeRateKesPerUsd = 130m;
    private string[] _navOrder = [];
    private UpdatePolicy _updatePolicy = UpdatePolicy.Prompt;
    private DateTime? _lastUpdateCheckUtc;
    private string? _skippedUpdateVersion;

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

    public int KesDecimalPlaces
    {
        get => _kesDecimalPlaces;
        set
        {
            var clamped = Math.Clamp(value, 0, 4);
            if (_kesDecimalPlaces == clamped) return;
            _kesDecimalPlaces = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KesPriceFormat));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int UsdDecimalPlaces
    {
        get => _usdDecimalPlaces;
        set
        {
            var clamped = Math.Clamp(value, 0, 4);
            if (_usdDecimalPlaces == clamped) return;
            _usdDecimalPlaces = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsdPriceFormat));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>KES per 1 USD. Prefer Currency table when available; kept for prefs fallback.</summary>
    public decimal ExchangeRateKesPerUsd
    {
        get => _exchangeRateKesPerUsd;
        set
        {
            var rate = value <= 0m ? 130m : value;
            if (_exchangeRateKesPerUsd == rate) return;
            _exchangeRateKesPerUsd = rate;
            OnPropertyChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string[] NavOrder
    {
        get => _navOrder;
        set
        {
            _navOrder = value ?? [];
            OnPropertyChanged();
        }
    }

    public UpdatePolicy UpdatePolicy
    {
        get => _updatePolicy;
        set
        {
            if (_updatePolicy == value) return;
            _updatePolicy = value;
            OnPropertyChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public DateTime? LastUpdateCheckUtc
    {
        get => _lastUpdateCheckUtc;
        set
        {
            if (_lastUpdateCheckUtc == value) return;
            _lastUpdateCheckUtc = value;
            OnPropertyChanged();
        }
    }

    public string? SkippedUpdateVersion
    {
        get => _skippedUpdateVersion;
        set
        {
            if (_skippedUpdateVersion == value) return;
            _skippedUpdateVersion = value;
            OnPropertyChanged();
        }
    }

    public string MoneyFormat => $"N{CostDecimalPlaces}";
    public string KesPriceFormat => $"N{KesDecimalPlaces}";
    public string UsdPriceFormat => $"N{UsdDecimalPlaces}";

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

    public string FormatKes(decimal value) =>
        value.ToString(KesPriceFormat, CultureInfo.CurrentCulture);

    public string FormatUsd(decimal value) =>
        value.ToString(UsdPriceFormat, CultureInfo.CurrentCulture);

    public decimal RoundMoney(decimal value) =>
        Math.Round(value, CostDecimalPlaces, MidpointRounding.AwayFromZero);

    public decimal ToUsd(decimal kesPerMt, decimal? rateOverride = null)
    {
        var rate = rateOverride ?? ExchangeRateKesPerUsd;
        return rate <= 0m
            ? 0m
            : Math.Round(kesPerMt / rate, UsdDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public void Save()
    {
        var path = FilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dto = new PrefsDto
        {
            CostDecimalPlaces = CostDecimalPlaces,
            KesDecimalPlaces = KesDecimalPlaces,
            UsdDecimalPlaces = UsdDecimalPlaces,
            ExchangeRateKesPerUsd = ExchangeRateKesPerUsd,
            NavOrder = NavOrder,
            UpdatePolicy = UpdatePolicy.ToString(),
            LastUpdateCheckUtc = LastUpdateCheckUtc,
            SkippedUpdateVersion = SkippedUpdateVersion
        };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    public void Load()
    {
        var path = FilePath();
        try
        {
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<PrefsDto>(json);
            if (dto is null) return;
            _costDecimalPlaces = Math.Clamp(dto.CostDecimalPlaces, 0, 4);
            _kesDecimalPlaces = json.Contains("KesDecimalPlaces", StringComparison.Ordinal)
                ? Math.Clamp(dto.KesDecimalPlaces, 0, 4)
                : 2;
            _usdDecimalPlaces = json.Contains("UsdDecimalPlaces", StringComparison.Ordinal)
                ? Math.Clamp(dto.UsdDecimalPlaces, 0, 4)
                : 2;
            _exchangeRateKesPerUsd = dto.ExchangeRateKesPerUsd > 0m ? dto.ExchangeRateKesPerUsd : 130m;
            _navOrder = dto.NavOrder ?? [];
            if (!string.IsNullOrWhiteSpace(dto.UpdatePolicy)
                && Enum.TryParse<UpdatePolicy>(dto.UpdatePolicy, ignoreCase: true, out var policy))
                _updatePolicy = policy;
            _lastUpdateCheckUtc = dto.LastUpdateCheckUtc;
            _skippedUpdateVersion = dto.SkippedUpdateVersion;
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
        public int KesDecimalPlaces { get; set; } = 2;
        public int UsdDecimalPlaces { get; set; } = 2;
        public decimal ExchangeRateKesPerUsd { get; set; } = 130m;
        public string[]? NavOrder { get; set; }
        public string? UpdatePolicy { get; set; }
        public DateTime? LastUpdateCheckUtc { get; set; }
        public string? SkippedUpdateVersion { get; set; }
    }
}
