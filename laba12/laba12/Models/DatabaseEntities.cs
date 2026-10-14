using SQLite;

namespace laba12.Models;

/// <summary>
/// Таблица 1: Путевые листы водителей и профиль смены на текущий день
/// </summary>
[Table("waybills")]
public class WaybillEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, MaxLength(50)]
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

    [MaxLength(50)]
    public string Status { get; set; } = "Активен"; // "Активен", "Завершен"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Таблица 2: Точки доставки маршрутного листа с флагом отложенной синхронизации
/// </summary>
[Table("delivery_points")]
public class DeliveryPointEntity
{
    [PrimaryKey, MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Indexed, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    [Indexed, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

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

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(50)]
    public string EstimatedTime { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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

    [Indexed, MaxLength(50)]
    public string DriverId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string DeliveryPointId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public double DistanceKm { get; set; }
    public double FuelLiters { get; set; }
    public double SpeedKmh { get; set; }

    [MaxLength(250)]
    public string EventDescription { get; set; } = string.Empty;
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
