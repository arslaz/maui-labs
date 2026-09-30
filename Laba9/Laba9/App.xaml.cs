using Laba9.Services;

namespace Laba9;

public partial class App : Application
{
    private readonly ISettingsService _settingsService;

    public App(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        // Автоматическое применение настроек при запуске приложения
        _settingsService.ApplyTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}