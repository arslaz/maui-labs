using Laba9.Services;
using Laba9.ViewModels;
using Laba9.Views;
using Microsoft.Extensions.Logging;

namespace Laba9;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Регистрация сервисов для работы с файлами и настройками
        builder.Services.AddSingleton<IDiaryStorageService, DiaryStorageService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        // Регистрация моделей представления (MVVM)
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<EntryDetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Регистрация страниц приложения
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<EntryDetailPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}