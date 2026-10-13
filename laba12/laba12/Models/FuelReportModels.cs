using System.Text.Json.Serialization;

namespace laba12.Models;

public class FuelReport
{
    [JsonPropertyName("reportId")]
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("driverId")]
    public string DriverId { get; set; } = string.Empty;

    [JsonPropertyName("driverName")]
    public string DriverName { get; set; } = string.Empty;

    [JsonPropertyName("vehicleNumber")]
    public string VehicleNumber { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("totalDistanceKm")]
    public double TotalDistanceKm { get; set; }

    [JsonPropertyName("fuelConsumedLiters")]
    public double FuelConsumedLiters { get; set; }

    [JsonPropertyName("fuelRemainingLiters")]
    public double FuelRemainingLiters { get; set; }

    [JsonPropertyName("averageLitersPer100Km")]
    public double AverageLitersPer100Km { get; set; }

    [JsonPropertyName("visitedOrders")]
    public List<string> VisitedOrders { get; set; } = new();

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class FuelReportUploadResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [JsonPropertyName("encryptionAlgorithm")]
    public string EncryptionAlgorithm { get; set; } = "AES-256-CBC";

    [JsonPropertyName("serverMessage")]
    public string ServerMessage { get; set; } = string.Empty;
}
