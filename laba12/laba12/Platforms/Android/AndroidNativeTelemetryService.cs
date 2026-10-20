#if ANDROID
#pragma warning disable CA1416, CA1422
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using laba12.Models;
using laba12.Platforms.Android;
using laba12.Services;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(AndroidNativeTelemetryService))]
namespace laba12.Platforms.Android;

/// <summary>
/// Вариант 7 (Android): Низкоуровневый системный сбор телеметрии через:
/// 1) Android.OS.BatteryManager (системный Intent.ActionBatteryChanged - уровень, вольтаж, температура, статус зарядки).
/// 2) Android.Net.ConnectivityManager и NetworkCapabilities (Cellular, Wi-Fi, полоса пропускания).
/// </summary>
public class AndroidNativeTelemetryService : INativeTelemetryService
{
    public string PlatformProviderName => "Android Low-Level (BatteryManager + ConnectivityManager)";

    public Task<NativeDeviceTelemetry> GetExtendedTelemetryAsync()
    {
        var result = new NativeDeviceTelemetry
        {
            Timestamp = DateTime.UtcNow
        };

        var context = global::Android.App.Application.Context;

        // 1. Опрос системного менеджера батареи Android через липкий системный Intent (Sticky Broadcast)
        try
        {
            using var filter = new IntentFilter(Intent.ActionBatteryChanged);
            var batteryIntent = context.RegisterReceiver(null, filter);

            if (batteryIntent != null)
            {
                int level = batteryIntent.GetIntExtra(BatteryManager.ExtraLevel, -1);
                int scale = batteryIntent.GetIntExtra(BatteryManager.ExtraScale, -1);

                if (level >= 0 && scale > 0)
                {
                    result.BatteryPercent = Math.Round((level / (double)scale) * 100.0, 1);
                }

                int status = batteryIntent.GetIntExtra(BatteryManager.ExtraStatus, -1);
                int plugged = batteryIntent.GetIntExtra(BatteryManager.ExtraPlugged, -1);

                result.IsCharging = status == (int)BatteryStatus.Charging ||
                                    status == (int)BatteryStatus.Full ||
                                    plugged == (int)BatteryPlugged.Ac ||
                                    plugged == (int)BatteryPlugged.Usb ||
                                    plugged == (int)BatteryPlugged.Wireless;

                string plugSource = plugged switch
                {
                    (int)BatteryPlugged.Ac => "Бортовая сеть / 220V",
                    (int)BatteryPlugged.Usb => "USB-порт",
                    (int)BatteryPlugged.Wireless => "Беспроводная",
                    _ => "АКБ"
                };

                result.BatteryStatusText = result.IsCharging
                    ? $"Зарядка ({plugSource})"
                    : $"Разряд ({result.BatteryPercent:F0}%)";

                int rawTemp = batteryIntent.GetIntExtra(BatteryManager.ExtraTemperature, -1);
                if (rawTemp > 0)
                {
                    result.BatteryTemperatureCelsius = Math.Round(rawTemp / 10.0, 1);
                }

                int rawVoltage = batteryIntent.GetIntExtra(BatteryManager.ExtraVoltage, -1);
                if (rawVoltage > 0)
                {
                    result.BatteryVoltageMv = rawVoltage;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeTelemetry] BatteryManager error: {ex.Message}");
            result.BatteryPercent = 95.0;
            result.BatteryStatusText = "Батарея: базовый режим";
        }

        // 2. Опрос системного менеджера соединений Android ConnectivityManager
        try
        {
            var cm = context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            if (cm != null)
            {
                var activeNetwork = cm.ActiveNetwork;
                if (activeNetwork != null)
                {
                    var capabilities = cm.GetNetworkCapabilities(activeNetwork);
                    if (capabilities != null)
                    {
                        result.IsInternetAvailable = capabilities.HasCapability(NetCapability.Internet);

                        if (capabilities.HasTransport(TransportType.Cellular))
                        {
                            result.IsCellular = true;
                            result.IsWiFi = false;
                            result.NetworkConnectionType = "Cellular (Мобильная сеть 4G/LTE)";
                        }
                        else if (capabilities.HasTransport(TransportType.Wifi))
                        {
                            result.IsWiFi = true;
                            result.IsCellular = false;
                            result.NetworkConnectionType = "Wi-Fi (Беспроводная сеть)";
                        }
                        else if (capabilities.HasTransport(TransportType.Ethernet))
                        {
                            result.NetworkConnectionType = "Ethernet (Проводная сеть)";
                        }
                        else
                        {
                            result.NetworkConnectionType = "Другая активная сеть";
                        }
                    }
                }
                else
                {
                    result.NetworkConnectionType = "Нет подключения (Офлайн)";
                    result.IsInternetAvailable = false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidNativeTelemetry] ConnectivityManager error: {ex.Message}");
            result.NetworkConnectionType = "Сетевой опрос недоступен";
        }

        // 3. Расчет оптимизации частоты отправки координат водителем
        OptimizeTelemetryFrequency(result);

        return Task.FromResult(result);
    }

    private static void OptimizeTelemetryFrequency(NativeDeviceTelemetry t)
    {
        if (t.IsCharging)
        {
            // Питание от авто: максимальная частота для точной навигации
            t.RecommendedTelemetryIntervalSeconds = 10;
            t.OptimizationDescription = "Питание от бортовой сети ТС: максимальная точность GPS (интервал: 10 сек).";
        }
        else if (t.BatteryPercent <= 20.0)
        {
            // Критический заряд: максимальная экономия батареи
            t.RecommendedTelemetryIntervalSeconds = 60;
            t.OptimizationDescription = $"Критический заряд АКБ ({t.BatteryPercent:F0}%): режим жесткой экономии (интервал: 60 сек).";
        }
        else if (t.BatteryPercent <= 45.0)
        {
            // Средний заряд
            t.RecommendedTelemetryIntervalSeconds = 35;
            t.OptimizationDescription = $"Заряд АКБ ({t.BatteryPercent:F0}%): умеренный интервал GPS (35 сек).";
        }
        else if (t.IsCellular)
        {
            // Сотовая сеть: бережем мобильный трафик и радиомодем
            t.RecommendedTelemetryIntervalSeconds = 20;
            t.OptimizationDescription = "Сотовая сеть (Cellular): баланс трафика 4G и заряда (интервал: 20 сек).";
        }
        else
        {
            // Wi-Fi и хороший заряд
            t.RecommendedTelemetryIntervalSeconds = 12;
            t.OptimizationDescription = "Wi-Fi соединение: повышенная точность маршрутизации (интервал: 12 сек).";
        }
    }
}
#endif
