namespace laba12.Models;

public class LocationPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Label { get; set; }

    public LocationPoint() { }

    public LocationPoint(double lat, double lon, string? label = null)
    {
        Latitude = lat;
        Longitude = lon;
        Label = label;
    }
}

public class RouteInfo
{
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public LocationPoint? StartPoint { get; set; }
    public List<LocationPoint> Geometry { get; set; } = new();
    public List<string> Instructions { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public string FormattedDistance => DistanceMeters >= 1000 
        ? $"{DistanceMeters / 1000.0:F1} км" 
        : $"{DistanceMeters:F0} м";

    public string FormattedDuration
    {
        get
        {
            var timeSpan = TimeSpan.FromSeconds(DurationSeconds);
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours} ч {timeSpan.Minutes} мин";
            }
            return $"{Math.Max(1, (int)timeSpan.TotalMinutes)} мин";
        }
    }
}
