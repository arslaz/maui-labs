using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using lab11.Resources.Languages;

namespace lab11.Services;

public class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _lazyInstance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _lazyInstance.Value;

    private const string LanguagePrefKey = "AppLanguage_Key";
    private CultureInfo _currentCulture = CultureInfo.InvariantCulture;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    private LocalizationManager()
    {
        InitializeCulture();
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set => SetCulture(value);
    }

    public FlowDirection CurrentFlowDirection =>
        IsRightToLeftCulture(_currentCulture) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public bool IsRtl => CurrentFlowDirection == FlowDirection.RightToLeft;

    public string CurrentLanguageCode => _currentCulture.TwoLetterISOLanguageName.ToLowerInvariant();

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return AppResources.GetString(key, _currentCulture);
    }

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(_currentCulture, culture))
            return;

        _currentCulture = culture;

        // Apply culture globally to current thread and default thread cultures
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Persist language selection in Preferences
        Preferences.Default.Set(LanguagePrefKey, culture.TwoLetterISOLanguageName);

        // Notify bindings: null notifies all properties including indexer bindings
        OnPropertyChanged(null);
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged(nameof(CurrentFlowDirection));
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(CurrentLanguageCode));

        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLanguageByCode(string languageCode)
    {
        CultureInfo targetCulture = languageCode.ToLowerInvariant() switch
        {
            "ru" => new CultureInfo("ru-RU"),
            "ar" => new CultureInfo("ar-SA"),
            _ => new CultureInfo("en-US")
        };

        SetCulture(targetCulture);
    }

    private void InitializeCulture()
    {
        // При каждом запуске/перезапуске приложения устанавливаем русский язык
        SetLanguageByCode("ru");
    }

    public static bool IsRightToLeftCulture(CultureInfo culture)
    {
        if (culture == null)
            return false;

        return culture.TextInfo.IsRightToLeft ||
               culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
