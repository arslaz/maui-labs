using System.Text.Json.Serialization;

namespace laba12.Models;

public class OsrmRootResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("routes")]
    public List<OsrmRoute>? Routes { get; set; }
}

public class OsrmRoute
{
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("geometry")]
    public OsrmGeometry? Geometry { get; set; }

    [JsonPropertyName("legs")]
    public List<OsrmLeg>? Legs { get; set; }
}

public class OsrmGeometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // GeoJSON coordinates format: [ [longitude, latitude], ... ]
    [JsonPropertyName("coordinates")]
    public List<List<double>>? Coordinates { get; set; }
}

public class OsrmLeg
{
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public List<OsrmStep>? Steps { get; set; }
}

public class OsrmStep
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}
