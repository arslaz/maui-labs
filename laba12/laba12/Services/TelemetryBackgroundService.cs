using laba12.Models;

namespace laba12.Services;

public interface ITelemetryService
{
    bool IsRunning { get; }
    double CurrentSpeedKmh { get; }
    double CurrentLatitude { get; }
    double CurrentLongitude { get; }
    int QueuedCount { get; }
    string StatusMessage { get; }
    DateTime? LastSentTime { get; }
    NativeDeviceTelemetry? CurrentNativeTelemetry { get; }

    event Action? OnTelemetryStatusChanged;

    void Start(string driverId);
    void Stop();
    Task<bool> SendImmediateAsync();
    Task<Location> GetCurrentDriverLocationAsync(CancellationToken ct = default);
    void UpdateDriverPosition(double lat, double lon);

    // Вариант 7: Работа с накопленной телеметрией в SQLite
    Task<SqliteTransactionResult> FlushOfflineWaypointsTransactionAsync(CancellationToken ct = default);
    Task<int> SimulateOfflineWaypointsAccumulationAsync(int count = 15);
}

public class TelemetryBackgroundService : ITelemetryService, IDisposable
{
    private readonly ILogisticsApiService _apiService;
    private readonly IDeviceLocationService _locationService;
    private readonly INativeTelemetryService _nativeTelemetryService;
    private readonly ILocalDatabaseService _databaseService;
    private CancellationTokenSource? _cts;
    private Task? _telemetryTask;
    private readonly object _lock = new();

    private string _driverId = "DRV-104";
    private double _currentLat = 53.9006;
    private double _currentLon = 27.5590;
    private double _currentSpeed = 54.0;
    private int _sqliteUnsyncedCount;
    private readonly Random _random = new();

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public double CurrentSpeedKmh => _currentSpeed;
    public double CurrentLatitude => _currentLat;
    public double CurrentLongitude => _currentLon;
    public int QueuedCount => _sqliteUnsyncedCount;
    public string StatusMessage { get; private set; } = "Телеметрия не запущена";
    public DateTime? LastSentTime { get; private set; }
    public NativeDeviceTelemetry? CurrentNativeTelemetry { get; private set; }

    public event Action? OnTelemetryStatusChanged;

    public TelemetryBackgroundService(
        ILogisticsApiService apiService,
        IDeviceLocationService locationService,
        INativeTelemetryService nativeTelemetryService,
        ILocalDatabaseService databaseService)
    {
        _apiService = apiService;
        _locationService = locationService;
        _nativeTelemetryService = nativeTelemetryService;
        _databaseService = databaseService;
    }

    public void Start(string driverId)
    {
        if (IsRunning) return;

        _driverId = driverId;
        _cts = new CancellationTokenSource();
        StatusMessage = "Фоновая телеметрия активна (интервал: 25-30 сек)";
        OnTelemetryStatusChanged?.Invoke();

        _telemetryTask = Task.Run(() => RunTelemetryLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            StatusMessage = "Фоновая телеметрия остановлена";
            OnTelemetryStatusChanged?.Invoke();
        }
        catch { }
    }

    private async Task RunTelemetryLoopAsync(CancellationToken ct)
    {
        try
        {
            await RefreshUnsyncedCountAsync();
            await SendSingleTelemetryAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                int delaySec = CurrentNativeTelemetry?.RecommendedTelemetryIntervalSeconds ?? 25;
                if (delaySec < 5) delaySec = 5;

                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);

                if (!ct.IsCancellationRequested)
                {
                    await SendSingleTelemetryAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryBackgroundService] Loop error: {ex.Message}");
        }
    }

    public void UpdateDriverPosition(double lat, double lon)
    {
        _currentLat = lat;
        _currentLon = lon;
        OnTelemetryStatusChanged?.Invoke();
    }

    public async Task<Location> GetCurrentDriverLocationAsync(CancellationToken ct = default)
    {
        try
        {
            var loc = await _locationService.GetCurrentLocationAsync(ct);
            if (loc != null)
            {
                _currentLat = loc.Latitude;
                _currentLon = loc.Longitude;
                if (loc.Speed.HasValue && loc.Speed.Value > 0)
                {
                    _currentSpeed = Math.Round(loc.Speed.Value * 3.6, 1);
                }
                return loc;
            }
        }
        catch { }

        return new Location(_currentLat, _currentLon);
    }

    public async Task<bool> SendImmediateAsync()
    {
        return await SendSingleTelemetryAsync(CancellationToken.None);
    }

    private async Task<bool> SendSingleTelemetryAsync(CancellationToken ct)
    {
        NativeDeviceTelemetry nativeTelemetry;
        try
        {
            nativeTelemetry = await _nativeTelemetryService.GetExtendedTelemetryAsync();
        }
        catch
        {
            nativeTelemetry = new NativeDeviceTelemetry();
        }
        CurrentNativeTelemetry = nativeTelemetry;

        try
        {
            var loc = await _locationService.GetCurrentLocationAsync(ct);
            if (loc != null)
            {
                _currentLat = loc.Latitude;
                _currentLon = loc.Longitude;
                if (loc.Speed.HasValue && loc.Speed.Value > 0)
                {
                    _currentSpeed = Math.Round(loc.Speed.Value * 3.6, 1);
                }
            }
            else
            {
                _currentLat += (_random.NextDouble() - 0.48) * 0.002;
                _currentLon += (_random.NextDouble() - 0.48) * 0.002;
                _currentSpeed = Math.Round(45.0 + _random.NextDouble() * 35.0, 1);
            }
        }
        catch
        {
            _currentLat += (_random.NextDouble() - 0.48) * 0.002;
            _currentLon += (_random.NextDouble() - 0.48) * 0.002;
            _currentSpeed = Math.Round(45.0 + _random.NextDouble() * 35.0, 1);
        }

        var isConnected = nativeTelemetry.IsInternetAvailable && Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        var telemetry = new TelemetryData
        {
            DriverId = _driverId,
            Latitude = _currentLat,
            Longitude = _currentLon,
            SpeedKmh = _currentSpeed,
            HeadingDegrees = _random.Next(0, 360),
            Timestamp = DateTime.UtcNow,
            BatteryPercent = (int)Math.Round(nativeTelemetry.BatteryPercent),
            NetworkStatus = nativeTelemetry.NetworkConnectionType
        };

        // ВАРИАНТ 7: Если сеть отсутствует — сохраняем путевую точку в SQLite (driver_waypoints)
        if (!isConnected)
        {
            await SaveWaypointToSqliteAsync(telemetry);
            await RefreshUnsyncedCountAsync();
            StatusMessage = $"Разрыв связи на трассе! Точка сохранена в SQLite (накоплено в базе: {QueuedCount}) | {nativeTelemetry.FormattedBattery}";
            OnTelemetryStatusChanged?.Invoke();
            return false;
        }

        bool success = false;
        try
        {
            success = await _apiService.SendTelemetryAsync(telemetry, ct);
        }
        catch (Exception)
        {
            success = false;
        }

        if (success)
        {
            LastSentTime = DateTime.Now;

            // ВАРИАНТ 7: При восстановлении связи — транзакционный сброс накопленных в SQLite точек
            if (_sqliteUnsyncedCount > 0)
            {
                var flushRes = await FlushOfflineWaypointsTransactionAsync(ct);
                StatusMessage = $"Связь восстановлена! {flushRes.Message}";
            }
            else
            {
                StatusMessage = $"POST-телеметрия: {LastSentTime:HH:mm:ss} | {nativeTelemetry.FormattedBattery} | {nativeTelemetry.FormattedNetwork}";
            }
        }
        else
        {
            // Сбой отправки — буферизируем в SQLite (Вариант 7)
            await SaveWaypointToSqliteAsync(telemetry);
            await RefreshUnsyncedCountAsync();
            StatusMessage = $"Сбой отправки на трассе! Точка пути записана в SQLite (в базе: {QueuedCount})";
        }

        OnTelemetryStatusChanged?.Invoke();
        return success;
    }

    /// <summary>
    /// ВАРИАНТ 7: Запись точки пути водителя в таблицу SQLite driver_waypoints
    /// с индексированием по координатам (Latitude, Longitude) и временному штампу (Timestamp)
    /// </summary>
    private async Task SaveWaypointToSqliteAsync(TelemetryData data)
    {
        try
        {
            var entity = new DriverWaypointEntity
            {
                DriverId = data.DriverId,
                Latitude = data.Latitude,
                Longitude = data.Longitude,
                Timestamp = data.Timestamp,
                SpeedKmh = data.SpeedKmh,
                HeadingDegrees = data.HeadingDegrees,
                BatteryPercent = data.BatteryPercent,
                NetworkStatus = data.NetworkStatus,
                IsSynced = false
            };

            await _databaseService.SaveOfflineWaypointAsync(entity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryBackgroundService] Ошибка записи в SQLite: {ex.Message}");
        }
    }

    private async Task RefreshUnsyncedCountAsync()
    {
        try
        {
            _sqliteUnsyncedCount = await _databaseService.GetUnsyncedWaypointsCountAsync(_driverId);
        }
        catch
        {
            _sqliteUnsyncedCount = 0;
        }
    }

    /// <summary>
    /// ВАРИАНТ 7: Считывание накопленной за время отсутствия сети телеметрии из SQLite,
    /// отправка в API и массовая отметка как синхронизированная строго внутри единой транзакции SQLite.
    /// </summary>
    public async Task<SqliteTransactionResult> FlushOfflineWaypointsTransactionAsync(CancellationToken ct = default)
    {
        var result = new SqliteTransactionResult
        {
            OperationType = "FLUSH_TELEMETRY_TRANSACTION",
            ExecutedAt = DateTime.Now
        };

        try
        {
            // 1. Считывание накопленных точек пути из SQLite (выборка по индексу IsSynced)
            var unsyncedWaypoints = await _databaseService.GetUnsyncedWaypointsAsync(_driverId);
            if (!unsyncedWaypoints.Any())
            {
                result.IsSuccess = true;
                result.IsCommitted = true;
                result.Message = "В локальной базе SQLite нет неотправленных точек пути.";
                return result;
            }

            var telemetryBatch = unsyncedWaypoints.Select(w => new TelemetryData
            {
                DriverId = w.DriverId,
                Latitude = w.Latitude,
                Longitude = w.Longitude,
                SpeedKmh = w.SpeedKmh,
                HeadingDegrees = w.HeadingDegrees,
                Timestamp = w.Timestamp,
                BatteryPercent = w.BatteryPercent,
                NetworkStatus = w.NetworkStatus
            }).ToList();

            // 2. Отправка пакета на сервер
            bool apiSuccess = await _apiService.SendBatchTelemetryAsync(telemetryBatch, ct);
            if (!apiSuccess)
            {
                result.IsSuccess = false;
                result.IsCommitted = false;
                result.Message = $"Сервер отклонил пакет телеметрии ({telemetryBatch.Count} точек). Данные сохранены в SQLite до следующей попытки.";
                return result;
            }

            // 3. АТОМАРНАЯ ТРАНЗАКЦИЯ SQLITE: массовая отметка точек как синхронизированных
            string batchId = $"BATCH-TRX-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            result = await _databaseService.MarkWaypointsAsSyncedInTransactionAsync(unsyncedWaypoints.Select(x => x.Id), batchId);

            await RefreshUnsyncedCountAsync();
            OnTelemetryStatusChanged?.Invoke();
            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.IsCommitted = false;
            result.ErrorDetails = ex.Message;
            result.Message = $"Ошибка сброса телеметрии: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Вариант 7 (Демо-метод для показа учителю): Имитация накопления десятков путевых точек пути водителя
    /// в офлайн-режиме с индексированием по координатам и времени.
    /// </summary>
    public async Task<int> SimulateOfflineWaypointsAccumulationAsync(int count = 15)
    {
        var waypoints = new List<DriverWaypointEntity>();
        var now = DateTime.UtcNow;
        double startLat = 53.8821;
        double startLon = 27.5942;

        for (int i = 0; i < count; i++)
        {
            waypoints.Add(new DriverWaypointEntity
            {
                DriverId = _driverId,
                Latitude = Math.Round(startLat + (i * 0.0035), 6),
                Longitude = Math.Round(startLon + ((_random.NextDouble() - 0.4) * 0.003), 6),
                Timestamp = now.AddMinutes(-count + i),
                SpeedKmh = Math.Round(42.0 + _random.NextDouble() * 30.0, 1),
                HeadingDegrees = 45 + (i * 5),
                BatteryPercent = 90 - (i / 3),
                NetworkStatus = "Offline (МКАД / Трасса)",
                IsSynced = false
            });
        }

        int saved = await _databaseService.SaveBatchOfflineWaypointsAsync(waypoints);
        await RefreshUnsyncedCountAsync();
        StatusMessage = $"Имитация офлайна: В SQLite сохранено {saved} точек пути водителя (всего в очереди: {QueuedCount})";
        OnTelemetryStatusChanged?.Invoke();
        return saved;
    }

    public void Dispose()
    {
        Stop();
    }
}
