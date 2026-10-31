using laba12.Services;
using laba12.ViewModels;
using laba12.Views;
using Microsoft.Extensions.Logging;

namespace laba12;

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

        // 1. DelegatingHandler для сквозной авторизации (Bearer Token + авто-обновление сессии через Refresh Token)
        builder.Services.AddTransient<AuthHeaderHandler>();

        // 2. HTTP Clients via IHttpClientFactory
        builder.Services.AddHttpClient("LogisticsClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler<AuthHeaderHandler>(); // Внедрение специализированного перехватчика в цепочку

        builder.Services.AddHttpClient("OsrmClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "MauiLogisticsNavigator/1.0");
        });

        builder.Services.AddSingleton<MockBackendServer>();
        builder.Services.AddSingleton<ISecureStorageService, SecureStorageService>();
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
        builder.Services.AddSingleton<ICacheService, FileCacheService>();
        builder.Services.AddSingleton<INetworkStatusService, NetworkStatusService>();
        builder.Services.AddSingleton<ISyncQueueService, SyncQueueService>();
        builder.Services.AddSingleton<IFuelReportService, FuelReportService>();
        builder.Services.AddSingleton<ILogisticsApiService, LogisticsApiService>();
        builder.Services.AddSingleton<IRoutingService, OsrmRoutingService>();
        builder.Services.AddSingleton<IDeviceLocationService, DeviceLocationService>();
        builder.Services.AddSingleton<ITelemetryService, TelemetryBackgroundService>();

        // 3. Нативные системные сервисы (DI + поддержка DependencyService)
#if ANDROID
        builder.Services.AddSingleton<INativeFeedbackService, laba12.Platforms.Android.AndroidNativeFeedbackService>();
        builder.Services.AddSingleton<INativeTelemetryService, laba12.Platforms.Android.AndroidNativeTelemetryService>();
        DependencyService.Register<INativeFeedbackService, laba12.Platforms.Android.AndroidNativeFeedbackService>();
        DependencyService.Register<INativeTelemetryService, laba12.Platforms.Android.AndroidNativeTelemetryService>();
#elif IOS
        builder.Services.AddSingleton<INativeFeedbackService, laba12.Platforms.iOS.IosNativeFeedbackService>();
        builder.Services.AddSingleton<INativeTelemetryService, laba12.Platforms.iOS.IosNativeTelemetryService>();
        DependencyService.Register<INativeFeedbackService, laba12.Platforms.iOS.IosNativeFeedbackService>();
        DependencyService.Register<INativeTelemetryService, laba12.Platforms.iOS.IosNativeTelemetryService>();
#elif MACCATALYST
        builder.Services.AddSingleton<INativeFeedbackService, laba12.Platforms.MacCatalyst.MacCatalystNativeFeedbackService>();
        builder.Services.AddSingleton<INativeTelemetryService, laba12.Platforms.MacCatalyst.MacCatalystNativeTelemetryService>();
        DependencyService.Register<INativeFeedbackService, laba12.Platforms.MacCatalyst.MacCatalystNativeFeedbackService>();
        DependencyService.Register<INativeTelemetryService, laba12.Platforms.MacCatalyst.MacCatalystNativeTelemetryService>();
#else
        builder.Services.AddSingleton<INativeFeedbackService, DefaultNativeFeedbackService>();
        builder.Services.AddSingleton<INativeTelemetryService, DefaultNativeTelemetryService>();
        DependencyService.Register<INativeFeedbackService, DefaultNativeFeedbackService>();
        DependencyService.Register<INativeTelemetryService, DefaultNativeTelemetryService>();
#endif


        // 4. ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<DeliveriesViewModel>();
        builder.Services.AddTransient<DeliveryDetailViewModel>();
        builder.Services.AddTransient<RouteMapViewModel>();

        // 5. Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<DeliveriesPage>();
        builder.Services.AddTransient<DeliveryDetailPage>();
        builder.Services.AddTransient<RouteMapPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}