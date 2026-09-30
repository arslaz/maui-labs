namespace Laba9.Services;

public class SettingsService : ISettingsService
{
    private const string KeyFontSize = "diary_font_size";
    private const string KeyTheme = "diary_theme";
    private const string KeySort = "diary_default_sort";

    public const string SortDateDesc = "По дате (сначала новые) 📅⬇";
    public const string SortDateAsc = "По дате (сначала старые) 📅⬆";
    public const string SortTitleAsc = "По названию (А-Я) 🔤";
    public const string SortUpdatedDesc = "По дате изменения 🔄";

    public static readonly List<string> AvailableSortOptions = new()
    {
        SortDateDesc,
        SortDateAsc,
        SortTitleAsc,
        SortUpdatedDesc
    };

    public static readonly List<string> AvailableThemes = new()
    {
        "Системная",
        "Светлая",
        "Тёмная"
    };

    public static readonly List<double> AvailableFontSizes = new()
    {
        13.0,
        15.0,
        17.0,
        19.0,
        21.0
    };

    public event Action? SettingsChanged;

    public double FontSize
    {
        get => Preferences.Default.Get(KeyFontSize, 16.0);
        set
        {
            if (Math.Abs(FontSize - value) > 0.1)
            {
                Preferences.Default.Set(KeyFontSize, value);
                SettingsChanged?.Invoke();
            }
        }
    }

    public string Theme
    {
        get => Preferences.Default.Get(KeyTheme, "Системная");
        set
        {
            if (Theme != value)
            {
                Preferences.Default.Set(KeyTheme, value);
                ApplyTheme();
                SettingsChanged?.Invoke();
            }
        }
    }

    public string DefaultSort
    {
        get => Preferences.Default.Get(KeySort, SortDateDesc);
        set
        {
            if (DefaultSort != value)
            {
                Preferences.Default.Set(KeySort, value);
                SettingsChanged?.Invoke();
            }
        }
    }

    public void ApplyTheme()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current == null) return;

            Application.Current.UserAppTheme = Theme switch
            {
                "Светлая" => AppTheme.Light,
                "Тёмная" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        });
    }

    public void ResetSettings()
    {
        Preferences.Default.Remove(KeyFontSize);
        Preferences.Default.Remove(KeyTheme);
        Preferences.Default.Remove(KeySort);
        ApplyTheme();
        SettingsChanged?.Invoke();
    }
}
