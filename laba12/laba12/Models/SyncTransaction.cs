using System.Text.Json.Serialization;

namespace laba12.Models;

public class SyncTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("deliveryId")]
    public string DeliveryId { get; set; } = string.Empty;

    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("httpMethod")]
    public string HttpMethod { get; set; } = string.Empty; // PATCH, PUT, DELETE

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("payloadJson")]
    public string? PayloadJson { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
