using System.Globalization;
using System.Text.Json;

namespace laba12.Services;

public interface IDeviceLocationService
{
    Task<Location> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
    Location? LastKnownLocation { get; }
}

public class DeviceLocationService : IDeviceLocationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private Location? _cachedLocation;

    public Location? LastKnownLocation => _cachedLocation;

    public DeviceLocationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Location> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        // 1. Check last known system location first (fastest, no GPS warm-up wait)
        try
        {
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown != null)
            {
                _cachedLocation = lastKnown;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] LastKnown error: {ex.Message}");
        }

        // 2. Request native device GPS position on MainThread (essential for MacCatalyst / iOS / Android permissions)
        Location? gpsLocation = null;
        try
        {
            if (MainThread.IsMainThread)
            {
                gpsLocation = await RequestGpsInternalAsync(cancellationToken);
            }
            else
            {
                gpsLocation = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await RequestGpsInternalAsync(cancellationToken);
                });
            }

            if (gpsLocation != null)
            {
                _cachedLocation = gpsLocation;
                return gpsLocation;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] Native GPS error: {ex.Message}");
        }

        // 3. If native GPS returned null, use cached location if available
        if (_cachedLocation != null)
        {
            return _cachedLocation;
        }

        // 4. Fallback: IP-based Geolocation (works when macOS TCC or emulator lacks GPS hardware)
        try
        {
            var ipLocation = await GetLocationFromIpAsync(cancellationToken);
            if (ipLocation != null)
            {
                _cachedLocation = ipLocation;
                return ipLocation;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] IP fallback error: {ex.Message}");
        }

        // 5. Ultimate fallback if completely offline and no GPS has ever succeeded:
        // Default to Minsk center (53.9006, 27.5590)
        return _cachedLocation ?? new Location(53.9006, 27.5590);
    }

    private async Task<Location?> RequestGpsInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                var req = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(6));
                return await Geolocation.Default.GetLocationAsync(req, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] RequestGpsInternal error: {ex.Message}");
        }

        return null;
    }

    private async Task<Location?> GetLocationFromIpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LogisticsClient");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            // Fast IP-based geolocation service
            var response = await client.GetAsync("http://ip-api.com/json/?fields=status,lat,lon,city", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var statusElem) && statusElem.GetString() == "success")
                {
                    if (root.TryGetProperty("lat", out var latElem) && root.TryGetProperty("lon", out var lonElem))
                    {
                        double lat = latElem.GetDouble();
                        double lon = lonElem.GetDouble();
                        System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] IP Geolocation resolved: {lat}, {lon}");
                        return new Location(lat, lon);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceLocationService] IP Geolocation error: {ex.Message}");
        }

        return null;
    }
}
