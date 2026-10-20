using laba12.Models;

namespace laba12.Services;

/// <summary>
/// Вариант 7: Нативный сервис сбора расширенной телеметрии.
/// Считывает точный уровень заряда батареи устройства и текущий тип сетевого соединения (Cellular/Wi-Fi)
/// через низкоуровневые системные менеджеры Android и iOS, оптимизируя частоту отправки координат.
/// </summary>
public interface INativeTelemetryService
{
    /// <summary>
    /// Опрашивает низкоуровневые системные менеджеры ОС и возвращает актуальные параметры телеметрии и сети
    /// </summary>
    Task<NativeDeviceTelemetry> GetExtendedTelemetryAsync();

    /// <summary>
    /// Название платформенного менеджера телеметрии (для отладки и прозрачности в UI)
    /// </summary>
    string PlatformProviderName { get; }
}
