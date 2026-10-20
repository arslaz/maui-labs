#if MACCATALYST
using laba12.Models;
using laba12.Platforms.MacCatalyst;
using laba12.Services;
using Network;

[assembly: Microsoft.Maui.Controls.Dependency(typeof(MacCatalystNativeTelemetryService))]
namespace laba12.Platforms.MacCatalyst;

public class MacCatalystNativeTelemetryService : INativeTelemetryService
{
    public string PlatformProviderName => "MacCatalyst (Apple Network Framework)";

    public Task<NativeDeviceTelemetry> GetExtendedTelemetryAsync()
    {
        var result = new NativeDeviceTelemetry
        {
            BatteryPercent = 100.0,
            IsCharging = true,
            BatteryStatusText = "Питание от сети (Mac)",
            IsWiFi = true,
            IsCellular = false,
            NetworkConnectionType = "Wi-Fi (WLAN)",
            IsInternetAvailable = true,
            RecommendedTelemetryIntervalSeconds = 10,
            OptimizationDescription = "MacCatalyst: Сетевое питание активно, максимальная точность (10 сек).",
            Timestamp = DateTime.UtcNow
        };

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
                    result.NetworkConnectionType = "Cellular (Мобильный модем)";
                    result.RecommendedTelemetryIntervalSeconds = 20;
                }
                else if (path.UsesInterfaceType(NWInterfaceType.Wifi))
                {
                    result.IsWiFi = true;
                    result.NetworkConnectionType = "Wi-Fi";
                }
                else if (path.UsesInterfaceType(NWInterfaceType.Wired))
                {
                    result.NetworkConnectionType = "Ethernet";
                }
            }
        }
        catch { }

        return Task.FromResult(result);
    }
}
#endif
