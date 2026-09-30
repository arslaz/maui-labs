namespace Laba9.Services;

public interface ISettingsService
{
    double FontSize { get; set; }
    string Theme { get; set; }
    string DefaultSort { get; set; }

    event Action? SettingsChanged;

    void ApplyTheme();
    void ResetSettings();
}
