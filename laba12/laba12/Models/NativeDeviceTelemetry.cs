namespace laba12.Models;

/// <summary>
/// Расширенная телеметрия устройства, собранная через низкоуровневые системные менеджеры ОС
/// (BatteryManager/UIDevice и ConnectivityManager/NWPathMonitor).
/// </summary>
public class NativeDeviceTelemetry
{
    /// <summary>
    /// Точный процент заряда батареи устройства (0 - 100%)
    /// </summary>
    public double BatteryPercent { get; set; } = 100.0;

    /// <summary>
    /// Флаг подключения к зарядному устройству (бортовая сеть ТС / адаптер)
    /// </summary>
    public bool IsCharging { get; set; }

    /// <summary>
    /// Статус аккумулятора ("Зарядка (USB)", "Разряд 84%", и т.д.)
    /// </summary>
    public string BatteryStatusText { get; set; } = "Норма";

    /// <summary>
    /// Температура батареи в °C (низкоуровневый сенсор Android)
    /// </summary>
    public double? BatteryTemperatureCelsius { get; set; }

    /// <summary>
    /// Напряжение аккумулятора в мВ (Android BatteryManager)
    /// </summary>
    public int? BatteryVoltageMv { get; set; }

    /// <summary>
    /// Тип активного сетевого подключения ("Cellular (4G/LTE)", "Wi-Fi (802.11)", "Ethernet")
    /// </summary>
    public string NetworkConnectionType { get; set; } = "Wi-Fi";

    /// <summary>
    /// Активно ли сотовое соединение передачи данных
    /// </summary>
    public bool IsCellular { get; set; }

    /// <summary>
    /// Активно ли Wi-Fi соединение
    /// </summary>
    public bool IsWiFi { get; set; }

    /// <summary>
    /// Подтверждена ли доступность интернета низкоуровневым сокетом/провайдером
    /// </summary>
    public bool IsInternetAvailable { get; set; } = true;

    /// <summary>
    /// Рекомендованный динамический интервал отправки гео-координат (в секундах)
    /// Рассчитывается во ViewModel/сервисе для баланса точности маршрута и энергопотребления
    /// </summary>
    public int RecommendedTelemetryIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Пояснение принятого решения по оптимизации частоты отправки
    /// </summary>
    public string OptimizationDescription { get; set; } = string.Empty;

    /// <summary>
    /// Время снятия показаний телеметрии
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Форматированная плашка для UI водителя
    /// </summary>
    public string FormattedBattery => IsCharging
        ? $"🔋 {BatteryPercent:F0}% ⚡ (Зарядка)"
        : $"🔋 {BatteryPercent:F0}%";

    /// <summary>
    /// Форматированная плашка сети
    /// </summary>
    public string FormattedNetwork => IsCellular
        ? $"📶 Cellular (4G/LTE)"
        : (IsWiFi ? $"📶 Wi-Fi" : $"📶 {NetworkConnectionType}");
}
