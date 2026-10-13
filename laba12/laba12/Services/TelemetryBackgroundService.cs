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

    event Action? OnTelemetryStatusChanged;

    void Start(string driverId);
    void Stop();
    Task<bool> SendImmediateAsync();
    Task<Location> GetCurrentDriverLocationAsync(CancellationToken ct = default);
    void UpdateDriverPosition(double lat, double lon);
}

public class TelemetryBackgroundService : ITelemetryService, IDisposable
{
    private readonly ILogisticsApiService _apiService;
    private readonly IDeviceLocationService _locationService;
    private CancellationTokenSource? _cts;
    private Task? _telemetryTask;
    private readonly Queue<TelemetryData> _offlineQueue = new();
    private readonly object _lock = new();

    private string _driverId = "DRV-104";
    private double _currentLat = 53.9006;
    private double _currentLon = 27.5590;
    private double _currentSpeed = 54.0;
    private readonly Random _random = new();

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public double CurrentSpeedKmh => _currentSpeed;
    public double CurrentLatitude => _currentLat;
    public double CurrentLongitude => _currentLon;
    public int QueuedCount
    {
        get { lock (_lock) { return _offlineQueue.Count; } }
    }
    public string StatusMessage { get; private set; } = "Телеметрия не запущена";
    public DateTime? LastSentTime { get; private set; }

    public event Action? OnTelemetryStatusChanged;

    public TelemetryBackgroundService(ILogisticsApiService apiService, IDeviceLocationService locationService)
    {
        _apiService = apiService;
        _locationService = locationService;
    }

    public void Start(string driverId)
    {
        if (IsRunning) return;

        _driverId = driverId;
        _cts = new CancellationTokenSource();
        StatusMessage = "Фоновая телеметрия активна (интервал: 30 сек)";
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
        // First ping immediately
        await SendSingleTelemetryAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                await SendSingleTelemetryAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
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
        // Try getting real GPS location from device location service
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
                // Fallback simulation: slight truck motion (100-300m)
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

        var isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        var telemetry = new TelemetryData
        {
            DriverId = _driverId,
            Latitude = _currentLat,
            Longitude = _currentLon,
            SpeedKmh = _currentSpeed,
            HeadingDegrees = _random.Next(0, 360),
            Timestamp = DateTime.UtcNow,
            BatteryPercent = Math.Max(20, 95 - _random.Next(0, 10)),
            NetworkStatus = isConnected ? "4G/LTE" : "Нет сети"
        };

        bool success = false;

        // If phone has NO internet connection, don't waste time on HTTP request - buffer immediately
        if (!isConnected)
        {
            lock (_lock)
            {
                _offlineQueue.Enqueue(telemetry);
                if (_offlineQueue.Count > 200) _offlineQueue.Dequeue();
            }
            StatusMessage = $"Разрыв связи на трассе! Пакет в буфере (накоплено: {QueuedCount})";
            OnTelemetryStatusChanged?.Invoke();
            return false;
        }

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
            
            // If we have buffered offline telemetry, flush it
            int flushedCount = 0;
            lock (_lock)
            {
                while (_offlineQueue.Count > 0)
                {
                    var queuedItem = _offlineQueue.Dequeue();
                    _ = _apiService.SendTelemetryAsync(queuedItem, CancellationToken.None);
                    flushedCount++;
                }
            }

            if (flushedCount > 0)
            {
                StatusMessage = $"Связь восстановлена! Передано {flushedCount} накопленных пакетов.";
            }
            else
            {
                StatusMessage = $"POST-телеметрия: {LastSentTime:HH:mm:ss} ({_currentSpeed:F0} км/ч, {_currentLat:F4}°N)";
            }
        }
        else
        {
            // Transient drop on highway
            lock (_lock)
            {
                _offlineQueue.Enqueue(telemetry);
                if (_offlineQueue.Count > 200) _offlineQueue.Dequeue();
            }
            StatusMessage = $"Сбой отправки на трассе! Пакет в буфере (накоплено: {QueuedCount})";
        }

        OnTelemetryStatusChanged?.Invoke();
        return success;
    }

    public void Dispose()
    {
        Stop();
    }
}
