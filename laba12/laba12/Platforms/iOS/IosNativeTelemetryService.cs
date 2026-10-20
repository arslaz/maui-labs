#if IOS
using laba12.Models;
using laba12.Platforms.iOS;
using laba12.Services;
using Network;
using UIKit;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(IosNativeTelemetryService))]
namespace laba12.Platforms.iOS;

/// <summary>
/// Вариант 7 (iOS): Низкоуровневый системный сбор телеметрии через:
/// 1) UIKit.UIDevice (BatteryMonitoringEnabled, BatteryLevel, BatteryState).
/// 2) Apple Network Framework (NWPathMonitor, NWInterfaceType.Cellular / Wifi).
/// </summary>
public class IosNativeTelemetryService : INativeTelemetryService
{
    public string PlatformProviderName => "Apple iOS (UIDevice + Network.NWPathMonitor)";

    public Task<NativeDeviceTelemetry> GetExtendedTelemetryAsync()
    {
        var result = new NativeDeviceTelemetry
        {
            Timestamp = DateTime.UtcNow
        };

        // 1. Опрос системного менеджера батареи Apple UIDevice
        try
        {
            UIDevice.CurrentDevice.BatteryMonitoringEnabled = true;
            float level = UIDevice.CurrentDevice.BatteryLevel;
            var state = UIDevice.CurrentDevice.BatteryState;

            if (level >= 0.0f)
            {
                result.BatteryPercent = Math.Round(level * 100.0, 1);
            }
            else
            {
                // На симуляторе iOS уровень батареи равен -1.0
                result.BatteryPercent = 95.0;
            }

            result.IsCharging = state == UIDeviceBatteryState.Charging || state == UIDeviceBatteryState.Full;

            result.BatteryStatusText = state switch
            {
                UIDeviceBatteryState.Charging => "Зарядка (Lightning/USB-C)",
                UIDeviceBatteryState.Full => "100% Заряжен",
                UIDeviceBatteryState.Unplugged => $"Разряд ({result.BatteryPercent:F0}%)",
                _ => "АКБ (iOS)"
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IosNativeTelemetry] UIDevice error: {ex.Message}");
            result.BatteryPercent = 90.0;
            result.BatteryStatusText = "UIDevice недоступен";
        }

        // 2. Низкоуровневый опрос системного Network Framework
        try
        {
            using var monitor = new NWPathMonitor();
            var path = monitor.CurrentPath;

            if (path != null)
            {
                result.IsInternetAvailable = path.Status == NWPathStatus.Satisfied;

                if (path.UsesInterfaceType(NWInterfaceType.Cellular))
                {
                    result.IsCellular = true;
                    result.IsWiFi = false;
                    result.NetworkConnectionType = "Cellular (Мобильная сотовая iOS)";
                }
                else if (path.UsesInterfaceType(NWInterfaceType.Wifi))
                {
                    result.IsWiFi = true;
                    result.IsCellular = false;
                    result.NetworkConnectionType = "Wi-Fi (Беспроводная сеть Apple)";
                }
                else if (path.UsesInterfaceType(NWInterfaceType.Wired))
                {
                    result.NetworkConnectionType = "Ethernet";
                }
                else
                {
                    result.NetworkConnectionType = result.IsInternetAvailable ? "Активная сеть iOS" : "Нет соединения";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IosNativeTelemetry] NWPathMonitor error: {ex.Message}");
            result.NetworkConnectionType = "Сетевой монитор недоступен";
        }

        // 3. Расчет частоты телеметрии
        OptimizeTelemetryFrequency(result);

        return Task.FromResult(result);
    }

    private static void OptimizeTelemetryFrequency(NativeDeviceTelemetry t)
    {
        if (t.IsCharging)
        {
            t.RecommendedTelemetryIntervalSeconds = 10;
            t.OptimizationDescription = "iOS: Питание подключено: максимальная частота координат (10 сек).";
        }
        else if (t.BatteryPercent <= 20.0)
        {
            t.RecommendedTelemetryIntervalSeconds = 60;
            t.OptimizationDescription = $"iOS: Критический уровень АКБ ({t.BatteryPercent:F0}%): эконом-режим (60 сек).";
        }
        else if (t.BatteryPercent <= 45.0)
        {
            t.RecommendedTelemetryIntervalSeconds = 35;
            t.OptimizationDescription = $"iOS: Заряд АКБ ({t.BatteryPercent:F0}%): умеренная частота (35 сек).";
        }
        else if (t.IsCellular)
        {
            t.RecommendedTelemetryIntervalSeconds = 20;
            t.OptimizationDescription = "iOS: Сотовая сеть Cellular: экономия трафика 4G/5G (20 сек).";
        }
        else
        {
            t.RecommendedTelemetryIntervalSeconds = 12;
            t.OptimizationDescription = "iOS: Wi-Fi соединение: высокая частота GPS (12 сек).";
        }
    }
}
#endif
