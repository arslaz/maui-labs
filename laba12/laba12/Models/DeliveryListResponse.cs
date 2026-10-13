using System.Text.Json.Serialization;

namespace laba12.Models;

public class DeliveryListResponse
{
    [JsonPropertyName("items")]
    public List<DeliveryPoint> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}
