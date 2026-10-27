using SQLite;

namespace laba12.Models;

/// <summary>
/// Таблица 1: Путевые листы водителей и профиль смены на текущий день
/// Общие требования: Индексация ключевых полей и уникальный номер путевого листа [Unique]
/// </summary>
[Table("waybills")]
public class WaybillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique, NotNull, MaxLength(50)]
    public string WaybillNumber { get; set; } = string.Empty;

    [Indexed, NotNull, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DriverName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string VehicleNumber { get; set; } = string.Empty;

    [Indexed]
    public DateTime WaybillDate { get; set; } = DateTime.Today;

    public double InitialOdometerKm { get; set; } = 145200.0;
    public double CurrentOdometerKm { get; set; } = 145285.4;
    public double FuelStartingLiters { get; set; } = 70.0;
    public double FuelConsumedLiters { get; set; } = 9.8;
    public double FuelRemainingLiters { get; set; } = 60.2;

    [Indexed, MaxLength(50)]
    public string Status { get; set; } = "Активен"; // "Активен", "Завершен"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Вариант 7 (Таблица справочника): Статический справочник категорий грузов и правил их транспортировки.
/// Поставляется в дистрибутиве приложения в папке Resources/Raw/app_local_storage.db3.
/// Жесткая реляционная связь «один-ко-многим» с таблицей delivery_points через внешний ключ CargoCategoryId.
/// </summary>
[Table("cargo_categories")]
public class CargoCategoryEntity
{
    [PrimaryKey, AutoIncrement]
    public int CategoryId { get; set; }

    [Unique, NotNull, MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;

    [NotNull, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [NotNull, MaxLength(100)]
    public string TemperatureMode { get; set; } = "Не регламентирован";

    [MaxLength(200)]
    public string HumidityAndVentilation { get; set; } = "Стандартная вентиляция";

    [MaxLength(500)]
    public string HandlingRequirements { get; set; } = string.Empty;

    [MaxLength(100)]
    public string SafetyClass { get; set; } = "Стандарт";

    public bool RequiresSeal { get; set; } = false;

    public int MaxStackTiers { get; set; } = 1;

    [MaxLength(20)]
    public string IconGlyph { get; set; } = "📦";
}

/// <summary>
/// Таблица 2: Точки доставки маршрутного листа
/// Вариант 7 + Общие требования:
/// - Жесткая реляционная связь с таблицей cargo_categories по внешнему ключу CargoCategoryId
/// - Уникальный серверный ID [Unique] и уникальный номер наряда [Unique]
/// - Индексы на координаты путевых точек (Latitude, Longitude) и временные штампы (UpdatedAt, ArrivedAt)
/// </summary>
[Table("delivery_points")]
public class DeliveryPointEntity
{
    [PrimaryKey, Unique, NotNull, MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Indexed, NotNull, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    [Indexed(Unique = true), Unique, NotNull, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    // Вариант 7: Внешний ключ жесткой реляционной связи с таблицей cargo_categories (Foreign Key)
    [Indexed, NotNull]
    public int CargoCategoryId { get; set; } = 1;

    [MaxLength(150)]
    public string ClientName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(50)]
    public string City { get; set; } = "Минск";

    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(250)]
    public string CargoDescription { get; set; } = string.Empty;

    public double WeightKg { get; set; }
    public double VolumeM3 { get; set; }

    [MaxLength(50)]
    public string Priority { get; set; } = "Обычный";

    [Indexed, MaxLength(50)]
    public string Status { get; set; } = "Ожидает"; // "Ожидает", "В пути", "Прибыл", "Отказ", "Доставлен"

    // Вариант 7: Индексы на координаты путевых точек
    [Indexed]
    public double Latitude { get; set; }

    [Indexed]
    public double Longitude { get; set; }

    [MaxLength(50)]
    public string EstimatedTime { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    // Вариант 7: Индексы на временные штампы
    [Indexed]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Indexed]
    public DateTime? ArrivedAt { get; set; }

    [MaxLength(300)]
    public string? RejectionReason { get; set; }

    // Специальные поля отложенной синхронизации SQLite
    [Indexed]
    public bool IsSyncPending { get; set; } = false;

    [MaxLength(50)]
    public string? PendingOperation { get; set; } // "PATCH_ARRIVAL", "PUT_REJECT", "DELETE"
}

/// <summary>
/// Таблица 3: Локальная таблица рейсов и расхода топлива для агрегации отчета
/// </summary>
[Table("trip_records")]
public class TripRecordEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    [Indexed, MaxLength(50)]
    public string DeliveryPointId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    // Вариант 7: Индекс на временные штампы поездки
    [Indexed]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public double DistanceKm { get; set; }
    public double FuelLiters { get; set; }
    public double SpeedKmh { get; set; }

    [MaxLength(250)]
    public string EventDescription { get; set; } = string.Empty;
}

/// <summary>
/// Вариант 7 (Таблица 4): Накопленная за время отсутствия сети телеметрия водителя.
/// «Установить индексы на координаты путевых точек и временные штампы поездки.
/// Накопленная за время отсутствия сети телеметрия (десятки точек пути водителя) при восстановлении связи
/// должна считываться из SQLite, отправляться в API и массово помечаться как синхронизированная
/// в рамках одной транзакции базы данных.»
/// </summary>
[Table("driver_waypoints")]
public class DriverWaypointEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    // Вариант 7: Индексы на координаты путевых точек
    [Indexed]
    public double Latitude { get; set; }

    [Indexed]
    public double Longitude { get; set; }

    // Вариант 7: Индекс на временные штампы поездки
    [Indexed]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public double SpeedKmh { get; set; }
    public double HeadingDegrees { get; set; }
    public int BatteryPercent { get; set; } = 85;

    [MaxLength(50)]
    public string NetworkStatus { get; set; } = "Offline";

    // Индекс для мгновенной выборки неотправленной телеметрии
    [Indexed]
    public bool IsSynced { get; set; } = false;

    [Indexed]
    public DateTime? SyncedAt { get; set; }

    [MaxLength(64)]
    public string? ServerBatchId { get; set; }
}

/// <summary>
/// DTO агрегированных данных из таблицы поездок для отчета о расходе топлива
/// </summary>
public class TripAggregatedStats
{
    public int TotalTrips { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalFuelConsumedLiters { get; set; }
    public double AverageConsumptionPer100Km { get; set; }
    public double AverageSpeedKmh { get; set; }
    public int DeliveredCount { get; set; }
    public int RejectedCount { get; set; }
}

/// <summary>
/// Результат проведения транзакции SQLite для визуальной индикации ACID в UI
/// </summary>
public class SqliteTransactionResult
{
    public bool IsSuccess { get; set; }
    public bool IsCommitted { get; set; }
    public int AffectedRows { get; set; }
    public long DurationMs { get; set; }
    public string OperationType { get; set; } = "TRANSACTION";
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Вариант 7: DTO результата выполнения связывающего SQL-запроса (JOIN)
/// между таблицей точек доставки delivery_points и справочником категорий cargo_categories.
/// Полностью исключает выполнение вложенных циклов и повторных запросов на стороне C#.
/// </summary>
public class DeliveryPointWithRegulationDto
{
    public string Id { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Минск";
    public string Phone { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public int CargoCategoryId { get; set; }
    public double WeightKg { get; set; }
    public double VolumeM3 { get; set; }
    public string Priority { get; set; } = "Обычный";
    public string Status { get; set; } = "Ожидает";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string EstimatedTime { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsSyncPending { get; set; }
    public string? PendingOperation { get; set; }

    // Поля, присоединенные через SQL JOIN из таблицы cargo_categories
    public string CargoCategoryCode { get; set; } = string.Empty;
    public string CargoCategoryName { get; set; } = string.Empty;
    public string CargoTemperatureMode { get; set; } = string.Empty;
    public string CargoHumidityAndVentilation { get; set; } = string.Empty;
    public string CargoHandlingRequirements { get; set; } = string.Empty;
    public string CargoSafetyClass { get; set; } = string.Empty;
    public bool CargoRequiresSeal { get; set; }
    public string CargoIconGlyph { get; set; } = "📦";
}

/// <summary>
/// Вариант 7: DTO агрегированных данных, извлеченных из СУБД с помощью сложного связывающего
/// SQL-запроса (JOIN), объединяющего данные справочника (cargo_categories),
/// маршрутного листа (delivery_points) и пользовательских транзакций (trip_records).
/// </summary>
public class CargoCategoryAggregatedReport
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string TemperatureMode { get; set; } = string.Empty;
    public string HandlingRequirements { get; set; } = string.Empty;
    public string SafetyClass { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "📦";
    public int TotalOrdersCount { get; set; }
    public double TotalWeightKg { get; set; }
    public double TotalVolumeM3 { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int RejectedOrdersCount { get; set; }
    public int InTransitOrdersCount { get; set; }
    public int PendingOrdersCount { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalFuelLiters { get; set; }
}
