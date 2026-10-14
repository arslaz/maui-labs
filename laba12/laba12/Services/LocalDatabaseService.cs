using System.Text.Json;
using laba12.Models;
using SQLite;

namespace laba12.Services;

public interface ILocalDatabaseService
{
    string DatabasePath { get; }
    Task InitAsync();
    Task<bool> MigrateFromLegacyJsonAsync(string driverId);
    Task<List<DeliveryPoint>> GetDeliveryPointsAsync(string driverId);
    Task SaveOrUpdateDeliveriesFromApiAsync(string driverId, IEnumerable<DeliveryPoint> points);
    Task MarkArrivalAsync(string deliveryId, DateTime arrivalTime, bool isOffline);
    Task MarkRejectAsync(string deliveryId, string reason, bool isOffline);
    Task DeleteDeliveryPointAsync(string deliveryId, bool isOffline);
    Task<List<DeliveryPointEntity>> GetPendingSyncItemsAsync(string driverId);
    Task ClearSyncPendingFlagAsync(string deliveryId);
    Task<WaybillEntity> GetOrCreateWaybillAsync(string driverId, string driverName, string vehicleNumber);
    Task<TripAggregatedStats> GetAggregatedTripStatisticsAsync(string driverId);
    Task<(int WaybillCount, int DeliveriesCount, int PendingSyncCount, int TripRecordsCount, string DbPath)> GetDatabaseSummaryAsync(string driverId);
}

public class LocalDatabaseService : ILocalDatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "app_local_storage.db3");

    public async Task InitAsync()
    {
        if (_database != null) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_database != null) return;

            // Инициализация пула соединений SQLite (Singleton pattern)
            _database = new SQLiteAsyncConnection(DatabasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            // Создание реляционных таблиц с декларативными атрибутами ORM
            await _database.CreateTableAsync<WaybillEntity>();
            await _database.CreateTableAsync<DeliveryPointEntity>();
            await _database.CreateTableAsync<TripRecordEntity>();

            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] SQLite база данных инициализирована: {DatabasePath}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Автоматическая миграция: считывание старых JSON-файлов кэша и запись в таблицы SQLite
    /// </summary>
    public async Task<bool> MigrateFromLegacyJsonAsync(string driverId)
    {
        await InitAsync();

        var migrationKey = $"sqlite_migration_completed_{driverId}";
        if (Preferences.Default.Get(migrationKey, false))
        {
            // Миграция уже выполнялась
            return false;
        }

        bool anyMigrated = false;

        try
        {
            // 1. Поиск старого файла deliveries_cache_{driverId}.json
            var oldCachePath = Path.Combine(FileSystem.AppDataDirectory, $"deliveries_cache_{driverId}.json");
            if (File.Exists(oldCachePath))
            {
                var json = await File.ReadAllTextAsync(oldCachePath);
                var oldList = JsonSerializer.Deserialize<List<DeliveryPoint>>(json, _jsonOptions);

                if (oldList != null && oldList.Any())
                {
                    foreach (var item in oldList)
                    {
                        var entity = MapToEntity(item, driverId);
                        await _database!.InsertOrReplaceAsync(entity);
                    }
                    anyMigrated = true;
                    System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] Миграция: {oldList.Count} записей перенесено из {Path.GetFileName(oldCachePath)} в таблицу delivery_points");
                }
            }

            // 2. Поиск старого файла pending_sync_queue.json
            var oldQueuePath = Path.Combine(FileSystem.AppDataDirectory, "pending_sync_queue.json");
            if (File.Exists(oldQueuePath))
            {
                var queueJson = await File.ReadAllTextAsync(oldQueuePath);
                var queue = JsonSerializer.Deserialize<List<SyncTransaction>>(queueJson, _jsonOptions);

                if (queue != null && queue.Any())
                {
                    foreach (var tx in queue)
                    {
                        var existing = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == tx.DeliveryId);
                        if (existing != null)
                        {
                            existing.IsSyncPending = true;
                            existing.PendingOperation = tx.HttpMethod;
                            await _database.UpdateAsync(existing);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] Миграция: {queue.Count} отложенных транзакций перенесено в SQLite флаги IsSyncPending");
                }
            }

            // 3. Создание стартового путевого листа и записей поездок, если пусто
            var waybillCount = await _database!.Table<WaybillEntity>().Where(x => x.DriverId == driverId).CountAsync();
            if (waybillCount == 0)
            {
                var waybill = new WaybillEntity
                {
                    DriverId = driverId,
                    DriverName = driverId == "DRV-105" ? "Дмитрий Ковалев" : "Алексей Соколов",
                    VehicleNumber = driverId == "DRV-105" ? "ВК 3829-7 (МАЗ-Купава)" : "АВ 7412-7 (ГАЗель NEXT)",
                    WaybillDate = DateTime.Today,
                    InitialOdometerKm = 145200.0,
                    CurrentOdometerKm = 145268.5,
                    FuelStartingLiters = 70.0,
                    FuelConsumedLiters = 8.2,
                    FuelRemainingLiters = 61.8,
                    Status = "Активен"
                };
                await _database.InsertAsync(waybill);

                // Создаем базовые записи рейсов для агрегации
                await _database.InsertAsync(new TripRecordEntity
                {
                    DriverId = driverId,
                    DeliveryPointId = "deliv-by-1",
                    OrderNumber = "ORD-BY-101",
                    Timestamp = DateTime.UtcNow.AddHours(-3),
                    DistanceKm = 18.5,
                    FuelLiters = 2.1,
                    SpeedKmh = 48.0,
                    EventDescription = "Выезд со склада на маршрут"
                });
                await _database.InsertAsync(new TripRecordEntity
                {
                    DriverId = driverId,
                    DeliveryPointId = "deliv-by-2",
                    OrderNumber = "ORD-BY-102",
                    Timestamp = DateTime.UtcNow.AddHours(-1.5),
                    DistanceKm = 14.2,
                    FuelLiters = 1.7,
                    SpeedKmh = 52.0,
                    EventDescription = "Проезд контрольной точки"
                });
            }

            Preferences.Default.Set(migrationKey, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] Ошибка миграции из JSON: {ex.Message}");
        }

        return anyMigrated;
    }

    /// <summary>
    /// Мгновенная загрузка данных из локальной базы данных SQLite для MVVM
    /// </summary>
    public async Task<List<DeliveryPoint>> GetDeliveryPointsAsync(string driverId)
    {
        await InitAsync();
        var entities = await _database!.Table<DeliveryPointEntity>()
            .Where(x => x.DriverId == driverId)
            .OrderBy(x => x.EstimatedTime)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <summary>
    /// Синхронизация данных, полученных из API, с локальной базой данных SQLite (Upsert)
    /// </summary>
    public async Task SaveOrUpdateDeliveriesFromApiAsync(string driverId, IEnumerable<DeliveryPoint> points)
    {
        await InitAsync();

        foreach (var p in points)
        {
            var existing = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == p.Id);
            if (existing != null)
            {
                // Не перезаписываем статус, если у нас есть локальное неотправленное изменение (IsSyncPending)
                if (!existing.IsSyncPending)
                {
                    existing.Status = p.Status;
                    existing.Notes = p.Notes;
                    existing.UpdatedAt = p.UpdatedAt;
                }
                existing.ClientName = p.ClientName;
                existing.Address = p.Address;
                existing.City = p.City;
                existing.Phone = p.Phone;
                existing.CargoDescription = p.CargoDescription;
                existing.WeightKg = p.WeightKg;
                existing.VolumeM3 = p.VolumeM3;
                existing.Priority = p.Priority;
                existing.Latitude = p.Latitude;
                existing.Longitude = p.Longitude;
                existing.EstimatedTime = p.EstimatedTime;

                await _database.UpdateAsync(existing);
            }
            else
            {
                var newEntity = MapToEntity(p, driverId);
                await _database.InsertAsync(newEntity);
            }
        }
    }

    /// <summary>
    /// Вариант 7: Контекстная команда «Отметить прибытие» (PATCH) — атомарно меняет строку в SQLite
    /// </summary>
    public async Task MarkArrivalAsync(string deliveryId, DateTime arrivalTime, bool isOffline)
    {
        await InitAsync();
        var existing = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == deliveryId);
        if (existing != null)
        {
            existing.Status = "Прибыл";
            existing.ArrivedAt = arrivalTime;
            existing.Notes = $"Отметка прибытия: {arrivalTime.ToLocalTime():HH:mm:ss} (зафиксировано в SQLite)";
            existing.UpdatedAt = arrivalTime;
            existing.IsSyncPending = isOffline;
            existing.PendingOperation = isOffline ? "PATCH_ARRIVAL" : null;

            await _database.UpdateAsync(existing);

            // Фиксация этапа рейса в таблице trip_records
            await _database.InsertAsync(new TripRecordEntity
            {
                DriverId = existing.DriverId,
                DeliveryPointId = existing.Id,
                OrderNumber = existing.OrderNumber,
                Timestamp = arrivalTime,
                DistanceKm = 8.4,
                FuelLiters = 0.95,
                SpeedKmh = 45.0,
                EventDescription = $"Прибытие на точку доставки {existing.OrderNumber}"
            });

            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] SQLite: обновлена строка {deliveryId} (Прибыл, IsSyncPending={isOffline})");
        }
    }

    /// <summary>
    /// Вариант 7: Контекстная команда «Зафиксировать отказ от груза» (PUT) — атомарно меняет строку в SQLite
    /// </summary>
    public async Task MarkRejectAsync(string deliveryId, string reason, bool isOffline)
    {
        await InitAsync();
        var existing = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == deliveryId);
        if (existing != null)
        {
            existing.Status = "Отказ";
            existing.RejectionReason = reason;
            existing.Notes = $"Акт отказа (SQLite): {reason}";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.IsSyncPending = isOffline;
            existing.PendingOperation = isOffline ? "PUT_REJECT" : null;

            await _database.UpdateAsync(existing);

            // Фиксация этапа рейса в таблице trip_records
            await _database.InsertAsync(new TripRecordEntity
            {
                DriverId = existing.DriverId,
                DeliveryPointId = existing.Id,
                OrderNumber = existing.OrderNumber,
                Timestamp = DateTime.UtcNow,
                DistanceKm = 2.0,
                FuelLiters = 0.25,
                SpeedKmh = 20.0,
                EventDescription = $"Отказ от груза {existing.OrderNumber}: {reason}"
            });

            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] SQLite: обновлена строка {deliveryId} (Отказ, IsSyncPending={isOffline})");
        }
    }

    /// <summary>
    /// Удаление ресурса из SQLite
    /// </summary>
    public async Task DeleteDeliveryPointAsync(string deliveryId, bool isOffline)
    {
        await InitAsync();
        var existing = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == deliveryId);
        if (existing != null)
        {
            if (isOffline)
            {
                // При офлайне помечаем строку для отложенного удаления на сервере
                existing.IsSyncPending = true;
                existing.PendingOperation = "DELETE";
                await _database.UpdateAsync(existing);
            }
            else
            {
                await _database.DeleteAsync(existing);
            }
            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] SQLite: удаление точки {deliveryId}");
        }
    }

    public async Task<List<DeliveryPointEntity>> GetPendingSyncItemsAsync(string driverId)
    {
        await InitAsync();
        return await _database!.Table<DeliveryPointEntity>()
            .Where(x => x.DriverId == driverId && x.IsSyncPending)
            .ToListAsync();
    }

    public async Task ClearSyncPendingFlagAsync(string deliveryId)
    {
        await InitAsync();
        var item = await _database!.Table<DeliveryPointEntity>().FirstOrDefaultAsync(x => x.Id == deliveryId);
        if (item != null)
        {
            if (item.PendingOperation == "DELETE")
            {
                await _database.DeleteAsync(item);
            }
            else
            {
                item.IsSyncPending = false;
                item.PendingOperation = null;
                await _database.UpdateAsync(item);
            }
        }
    }

    public async Task<WaybillEntity> GetOrCreateWaybillAsync(string driverId, string driverName, string vehicleNumber)
    {
        await InitAsync();
        var waybill = await _database!.Table<WaybillEntity>()
            .Where(x => x.DriverId == driverId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (waybill == null)
        {
            waybill = new WaybillEntity
            {
                DriverId = driverId,
                DriverName = driverName,
                VehicleNumber = vehicleNumber,
                WaybillDate = DateTime.Today,
                Status = "Активен"
            };
            await _database.InsertAsync(waybill);
        }

        return waybill;
    }

    /// <summary>
    /// Вариант 7: Агрегация данных из локальной таблицы поездок trip_records для отчета о расходе топлива
    /// </summary>
    public async Task<TripAggregatedStats> GetAggregatedTripStatisticsAsync(string driverId)
    {
        await InitAsync();

        var trips = await _database!.Table<TripRecordEntity>()
            .Where(x => x.DriverId == driverId)
            .ToListAsync();

        var deliveries = await _database.Table<DeliveryPointEntity>()
            .Where(x => x.DriverId == driverId)
            .ToListAsync();

        double totalDist = trips.Sum(x => x.DistanceKm);
        double totalFuel = trips.Sum(x => x.FuelLiters);
        double avgSpeed = trips.Any() ? trips.Average(x => x.SpeedKmh) : 48.0;

        // Если записей рейсов мало, используем базовые расчетные значения поездки
        if (totalDist < 10.0) totalDist = 48.5;
        if (totalFuel < 1.0) totalFuel = 5.5;

        double avgPer100 = totalDist > 0 ? Math.Round((totalFuel / totalDist) * 100.0, 1) : 11.4;

        return new TripAggregatedStats
        {
            TotalTrips = trips.Count,
            TotalDistanceKm = Math.Round(totalDist, 1),
            TotalFuelConsumedLiters = Math.Round(totalFuel, 2),
            AverageConsumptionPer100Km = avgPer100,
            AverageSpeedKmh = Math.Round(avgSpeed, 1),
            DeliveredCount = deliveries.Count(x => x.Status == "Доставлен" || x.Status == "Прибыл"),
            RejectedCount = deliveries.Count(x => x.Status == "Отказ")
        };
    }

    public async Task<(int WaybillCount, int DeliveriesCount, int PendingSyncCount, int TripRecordsCount, string DbPath)> GetDatabaseSummaryAsync(string driverId)
    {
        await InitAsync();
        int waybills = await _database!.Table<WaybillEntity>().Where(x => x.DriverId == driverId).CountAsync();
        int deliveries = await _database.Table<DeliveryPointEntity>().Where(x => x.DriverId == driverId && x.PendingOperation != "DELETE").CountAsync();
        int pending = await _database.Table<DeliveryPointEntity>().Where(x => x.DriverId == driverId && x.IsSyncPending).CountAsync();
        int trips = await _database.Table<TripRecordEntity>().Where(x => x.DriverId == driverId).CountAsync();

        return (waybills, deliveries, pending, trips, DatabasePath);
    }

    private static DeliveryPointEntity MapToEntity(DeliveryPoint m, string driverId) => new()
    {
        Id = m.Id,
        DriverId = driverId,
        OrderNumber = m.OrderNumber,
        ClientName = m.ClientName,
        Address = m.Address,
        City = m.City,
        Phone = m.Phone,
        CargoDescription = m.CargoDescription,
        WeightKg = m.WeightKg,
        VolumeM3 = m.VolumeM3,
        Priority = m.Priority,
        Status = m.Status,
        Latitude = m.Latitude,
        Longitude = m.Longitude,
        EstimatedTime = m.EstimatedTime,
        Notes = m.Notes,
        UpdatedAt = m.UpdatedAt,
        IsSyncPending = m.IsSyncPending
    };

    private static DeliveryPoint MapToModel(DeliveryPointEntity e) => new()
    {
        Id = e.Id,
        OrderNumber = e.OrderNumber,
        ClientName = e.ClientName,
        Address = e.Address,
        City = e.City,
        Phone = e.Phone,
        CargoDescription = e.CargoDescription,
        WeightKg = e.WeightKg,
        VolumeM3 = e.VolumeM3,
        Priority = e.Priority,
        Status = e.Status,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        EstimatedTime = e.EstimatedTime,
        Notes = e.Notes,
        UpdatedAt = e.UpdatedAt,
        IsSyncPending = e.IsSyncPending
    };
}
