using System.Globalization;
using System.Text.Json;
using laba12.Models;

namespace laba12.Services;

public interface IRoutingService
{
    Task<RouteInfo> CalculateRouteAsync(double startLat, double startLon, IEnumerable<DeliveryPoint> waypoints, CancellationToken cancellationToken = default);
}

public class OsrmRoutingService : IRoutingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OsrmRoutingService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RouteInfo> CalculateRouteAsync(double startLat, double startLon, IEnumerable<DeliveryPoint> waypoints, CancellationToken cancellationToken = default)
    {
        var waypointsList = waypoints.ToList();
        if (!waypointsList.Any())
        {
            return new RouteInfo
            {
                IsSuccess = false,
                ErrorMessage = "Список путевых точек пуст."
            };
        }

        // Coordinates format for OSRM: {lon},{lat};{lon},{lat};...
        var coordParts = new List<string>
        {
            $"{startLon.ToString("F6", CultureInfo.InvariantCulture)},{startLat.ToString("F6", CultureInfo.InvariantCulture)}"
        };

        foreach (var p in waypointsList)
        {
            coordParts.Add($"{p.Longitude.ToString("F6", CultureInfo.InvariantCulture)},{p.Latitude.ToString("F6", CultureInfo.InvariantCulture)}");
        }

        var coordsQuery = string.Join(";", coordParts);
        var url = $"http://router.project-osrm.org/route/v1/driving/{coordsQuery}?overview=full&geometries=geojson&steps=true";

        try
        {
            var client = _httpClientFactory.CreateClient("OsrmClient");
            client.Timeout = TimeSpan.FromSeconds(12);

            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var osrmData = JsonSerializer.Deserialize<OsrmRootResponse>(json, _jsonOptions);

            if (osrmData != null && osrmData.Routes != null && osrmData.Routes.Count > 0)
            {
                var route = osrmData.Routes[0];
                var routeInfo = new RouteInfo
                {
                    DistanceMeters = route.Distance,
                    DurationSeconds = route.Duration,
                    StartPoint = new LocationPoint(startLat, startLon, "Текущее положение водителя"),
                    IsSuccess = true
                };

                // Parse GeoJSON coordinates: array of [lon, lat]
                if (route.Geometry?.Coordinates != null)
                {
                    foreach (var pair in route.Geometry.Coordinates)
                    {
                        if (pair.Count >= 2)
                        {
                            // In GeoJSON, pair[0] is Lon, pair[1] is Lat
                            routeInfo.Geometry.Add(new LocationPoint(pair[1], pair[0]));
                        }
                    }
                }

                // Parse steps / instructions
                if (route.Legs != null)
                {
                    int legNum = 1;
                    foreach (var leg in route.Legs)
                    {
                        var targetName = legNum <= waypointsList.Count ? waypointsList[legNum - 1].Address : $"Точка {legNum}";
                        routeInfo.Instructions.Add($"Этап {legNum}: к «{targetName}» — {leg.Distance / 1000.0:F1} км ({TimeSpan.FromSeconds(leg.Duration).Minutes} мин)");
                        legNum++;
                    }
                }

                return routeInfo;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OsrmRoutingService] OSRM request failed: {ex.Message}. Using fallback interpolation.");
        }

        // Fallback realistic interpolation when offline or OSRM unavailable
        return GenerateFallbackRoute(startLat, startLon, waypointsList);
    }

    private RouteInfo GenerateFallbackRoute(double startLat, double startLon, List<DeliveryPoint> waypoints)
    {
        var points = new List<LocationPoint> { new(startLat, startLon, "Старт (База)") };
        double totalDistance = 0;
        var prevLat = startLat;
        var prevLon = startLon;

        for (int i = 0; i < waypoints.Count; i++)
        {
            var p = waypoints[i];
            
            // Add intermediate interpolated points for a curved realistic trajectory
            int steps = 5;
            for (int s = 1; s <= steps; s++)
            {
                double t = (double)s / steps;
                double lat = prevLat + (p.Latitude - prevLat) * t;
                double lon = prevLon + (p.Longitude - prevLon) * t;
                points.Add(new LocationPoint(lat, lon));
            }

            // Approximate distance
            var dist = CalculateHaversineMeters(prevLat, prevLon, p.Latitude, p.Longitude);
            totalDistance += dist;
            prevLat = p.Latitude;
            prevLon = p.Longitude;
        }

        var info = new RouteInfo
        {
            DistanceMeters = totalDistance * 1.3, // factor for road detour
            DurationSeconds = (totalDistance * 1.3 / (40.0 * 1000 / 3600)), // avg 40 km/h
            StartPoint = new LocationPoint(startLat, startLon, "Текущее положение водителя (Офлайн)"),
            Geometry = points,
            IsSuccess = true,
            ErrorMessage = "Маршрут построен в офлайн-режиме (прямая аппроксимация)"
        };

        for (int i = 0; i < waypoints.Count; i++)
        {
            info.Instructions.Add($"Этап {i + 1}: следование к {waypoints[i].Address}");
        }

        return info;
    }

    private static double CalculateHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
