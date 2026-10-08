using System.Globalization;

namespace lab11.Services;

public enum PluralCategory
{
    Zero,
    One,
    Two,
    Few,
    Many,
    Other
}

public interface IPluralizationService
{
    PluralCategory GetPluralCategory(int count, CultureInfo culture);
    string GetPluralizedString(string prefix, int count, CultureInfo culture);
}

public class PluralizationService : IPluralizationService
{
    private static readonly Lazy<PluralizationService> _lazy = new(() => new PluralizationService());
    public static PluralizationService Instance => _lazy.Value;

    public PluralCategory GetPluralCategory(int count, CultureInfo culture)
    {
        var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        var n = Math.Abs(count);

        return lang switch
        {
            "ru" => GetRussianPluralCategory(n),
            "ar" => GetArabicPluralCategory(n),
            _ => GetEnglishPluralCategory(n)
        };
    }

    /// <summary>
    /// English CLDR Plural Rules:
    /// One: n == 1
    /// Other: all other counts
    /// </summary>
    private static PluralCategory GetEnglishPluralCategory(int n)
    {
        return n == 1 ? PluralCategory.One : PluralCategory.Other;
    }

    /// <summary>
    /// Russian / Slavic CLDR Plural Rules:
    /// One: n % 10 == 1 and n % 100 != 11 (1, 21, 31, 101...)
    /// Few: n % 10 in 2..4 and n % 100 not in 12..14 (2..4, 22..24...)
    /// Many: n % 10 == 0 or n % 10 in 5..9 or n % 100 in 11..14 (0, 5..20, 25..30...)
    /// </summary>
    private static PluralCategory GetRussianPluralCategory(int n)
    {
        int mod10 = n % 10;
        int mod100 = n % 100;

        if (mod10 == 1 && mod100 != 11)
            return PluralCategory.One;

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
            return PluralCategory.Few;

        return PluralCategory.Many;
    }

    /// <summary>
    /// Arabic CLDR Plural Rules (6 forms):
    /// Zero: n == 0
    /// One: n == 1
    /// Two: n == 2
    /// Few: n % 100 in 3..10 (3..10, 103..110...)
    /// Many: n % 100 in 11..99 (11..99, 111..199...)
    /// Other: 100, 200, 1000...
    /// </summary>
    private static PluralCategory GetArabicPluralCategory(int n)
    {
        if (n == 0) return PluralCategory.Zero;
        if (n == 1) return PluralCategory.One;
        if (n == 2) return PluralCategory.Two;

        int mod100 = n % 100;
        if (mod100 >= 3 && mod100 <= 10)
            return PluralCategory.Few;

        if (mod100 >= 11 && mod100 <= 99)
            return PluralCategory.Many;

        return PluralCategory.Other;
    }

    public string GetPluralizedString(string prefix, int count, CultureInfo culture)
    {
        var category = GetPluralCategory(count, culture);
        var manager = LocalizationManager.Instance;

        // Try exact key: e.g. "Plural_Minutes_Few"
        string resourceKey = $"{prefix}_{category}";
        string template = manager.GetString(resourceKey);

        // Fallbacks if specific key isn't provided in resource file
        if (string.IsNullOrEmpty(template) || template == resourceKey)
        {
            if (category == PluralCategory.Many || category == PluralCategory.Few)
            {
                template = manager.GetString($"{prefix}_Other");
            }
            if (string.IsNullOrEmpty(template) || template == $"{prefix}_Other")
            {
                template = manager.GetString($"{prefix}_One");
            }
        }

        if (string.IsNullOrEmpty(template) || template == $"{prefix}_One")
        {
            return $"{count}";
        }

        // Format number in culture
        string formattedNumber = FormatLocalizedNumber(count, culture);

        try
        {
            return string.Format(culture, template, formattedNumber);
        }
        catch
        {
            return string.Format(template, formattedNumber);
        }
    }

    public static string FormatLocalizedNumber(int value, CultureInfo culture)
    {
        // For Arabic, optionally render in Eastern Arabic-Indic numerals for authentic feel
        if (culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertToEasternArabicNumerals(value.ToString(culture));
        }

        return value.ToString("N0", culture);
    }

    public static string ConvertToEasternArabicNumerals(string input)
    {
        char[] arabicDigits = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];
        char[] result = new char[input.Length];

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c >= '0' && c <= '9')
            {
                result[i] = arabicDigits[c - '0'];
            }
            else
            {
                result[i] = c;
            }
        }

        return new string(result);
    }
}
