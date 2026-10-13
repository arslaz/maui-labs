using System.Text.Json.Serialization;

namespace laba12.Models;

public class TelemetryData
{
    [JsonPropertyName("driverId")]
    public string DriverId { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("speedKmh")]
    public double SpeedKmh { get; set; }

    [JsonPropertyName("headingDegrees")]
    public double HeadingDegrees { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("batteryPercent")]
    public int BatteryPercent { get; set; } = 85;

    [JsonPropertyName("networkStatus")]
    public string NetworkStatus { get; set; } = "4G/LTE";
}
