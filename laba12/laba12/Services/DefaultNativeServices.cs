using laba12.Models;

namespace laba12.Services;

public class DefaultNativeFeedbackService : INativeFeedbackService
{
    public Task TriggerSuccessFeedbackAsync() => Task.CompletedTask;
    public Task TriggerItemDeletedFeedbackAsync() => Task.CompletedTask;
    public Task TriggerWarningFeedbackAsync() => Task.CompletedTask;
    public Task TriggerClickFeedbackAsync() => Task.CompletedTask;

    public Task ShowNativeToastAsync(string message, bool isLong = false)
    {
        System.Diagnostics.Debug.WriteLine($"[Toast] {message}");
        return Task.CompletedTask;
    }
}

public class DefaultNativeTelemetryService : INativeTelemetryService
{
    public string PlatformProviderName => "Default Fallback Provider";

    public Task<NativeDeviceTelemetry> GetExtendedTelemetryAsync()
    {
        return Task.FromResult(new NativeDeviceTelemetry
        {
            BatteryPercent = 95.0,
            IsCharging = true,
            BatteryStatusText = "Питание стабильное",
            NetworkConnectionType = "Wi-Fi",
            IsWiFi = true,
            RecommendedTelemetryIntervalSeconds = 15,
            OptimizationDescription = "Стандартный интервал телеметрии (15 сек).",
            Timestamp = DateTime.UtcNow
        });
    }
}
