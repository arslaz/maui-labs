using System.Globalization;

namespace lab11.Services;

public enum DistanceUnitMode
{
    Auto,
    Meters,
    Yards,
    Steps
}

public interface IRegionalFormatService
{
    DistanceUnitMode CurrentMode { get; set; }
    string FormatDistance(int distanceInMeters, CultureInfo culture);
    string FormatDateTime(DateTime dateTime, CultureInfo culture);
    string FormatCurrency(decimal amount, CultureInfo culture);
    string FormatNumber(int number, CultureInfo culture);
}

public class RegionalFormatService : IRegionalFormatService
{
    private const string DistancePrefKey = "AppDistance_Mode";
    private static readonly Lazy<RegionalFormatService> _lazy = new(() => new RegionalFormatService());
    public static RegionalFormatService Instance => _lazy.Value;

    private DistanceUnitMode _currentMode = DistanceUnitMode.Auto;

    private RegionalFormatService()
    {
        string saved = Preferences.Default.Get(DistancePrefKey, DistanceUnitMode.Auto.ToString());
        if (Enum.TryParse<DistanceUnitMode>(saved, out var parsed))
        {
            _currentMode = parsed;
        }
    }

    public DistanceUnitMode CurrentMode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            Preferences.Default.Set(DistancePrefKey, value.ToString());
        }
    }

    public string FormatDistance(int distanceInMeters, CultureInfo culture)
    {
        var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        var effectiveUnit = ResolveEffectiveUnit(_currentMode, lang);

        switch (effectiveUnit)
        {
            case DistanceUnitMode.Yards:
            {
                int yards = (int)Math.Round(distanceInMeters * 1.09361);
                string localizedNum = PluralizationService.FormatLocalizedNumber(yards, culture);
                return lang switch
                {
                    "ru" => $"{localizedNum} ярд",
                    "ar" => $"{localizedNum} ياردة",
                    _ => $"{localizedNum} yd"
                };
            }
            case DistanceUnitMode.Steps:
            {
                int steps = (int)Math.Round(distanceInMeters * 1.35);
                string localizedNum = PluralizationService.FormatLocalizedNumber(steps, culture);
                return lang switch
                {
                    "ru" => $"{localizedNum} шагов",
                    "ar" => $"{localizedNum} خطوة",
                    _ => $"{localizedNum} steps"
                };
            }
            case DistanceUnitMode.Meters:
            default:
            {
                string localizedNum = PluralizationService.FormatLocalizedNumber(distanceInMeters, culture);
                return lang switch
                {
                    "ru" => $"{localizedNum} м",
                    "ar" => $"{localizedNum} م",
                    _ => $"{localizedNum} m"
                };
            }
        }
    }

    private static DistanceUnitMode ResolveEffectiveUnit(DistanceUnitMode mode, string lang)
    {
        if (mode != DistanceUnitMode.Auto)
            return mode;

        // Auto cultural defaults:
        // English (US/UK) prefers yards / imperial
        // Russian and Arabic prefer metric meters
        return lang switch
        {
            "en" => DistanceUnitMode.Yards,
            "ru" => DistanceUnitMode.Meters,
            "ar" => DistanceUnitMode.Meters,
            _ => DistanceUnitMode.Meters
        };
    }

    public string FormatDateTime(DateTime dateTime, CultureInfo culture)
    {
        try
        {
            // Formats with full month name, day, year and time
            return dateTime.ToString("dd MMMM yyyy, HH:mm", culture);
        }
        catch
        {
            return dateTime.ToString("F", CultureInfo.InvariantCulture);
        }
    }

    public string FormatCurrency(decimal amount, CultureInfo culture)
    {
        var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();

        // Standardize amounts and regional symbols based on culture
        switch (lang)
        {
            case "ru":
                return $"{amount:N2} ₽";
            case "ar":
                // Arab Emirates Dirham / Egyptian Pound regional format
                var arabicNumber = PluralizationService.ConvertToEasternArabicNumerals($"{amount:N2}");
                return $"{arabicNumber} د.إ";
            case "en":
            default:
                return $"${amount:N2}";
        }
    }

    public string FormatNumber(int number, CultureInfo culture)
    {
        return PluralizationService.FormatLocalizedNumber(number, culture);
    }
}
