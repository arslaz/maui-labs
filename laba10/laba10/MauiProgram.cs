using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using laba10.Services;
using laba10.ViewModels;
using laba10.Views;

namespace laba10;

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

        builder.AddAudio();

        // 1. Регистрация сервисов подсистем хранения
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ITestService, TestService>();
        builder.Services.AddSingleton<IResultService, ResultService>();

        // 2. Регистрация ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ExamViewModel>();
        builder.Services.AddTransient<SettingsAndStatsViewModel>();
        builder.Services.AddTransient<TeacherEditViewModel>();

        // 3. Регистрация Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ExamPage>();
        builder.Services.AddTransient<SettingsAndStatsPage>();
        builder.Services.AddTransient<TeacherEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}