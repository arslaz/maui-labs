using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace laba12.Models;

public partial class DeliveryPoint : ObservableObject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = "Минск";

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("cargoDescription")]
    public string CargoDescription { get; set; } = string.Empty;

    // Вариант 7: Внешний ключ жесткой реляционной связи с таблицей cargo_categories (Foreign Key)
    [JsonPropertyName("cargoCategoryId")]
    public int CargoCategoryId { get; set; } = 1;

    // Поля, подтягиваемые из справочника категорий через связывающий SQL JOIN
    [JsonPropertyName("cargoCategoryCode")]
    public string CargoCategoryCode { get; set; } = string.Empty;

    [JsonPropertyName("cargoCategoryName")]
    public string CargoCategoryName { get; set; } = string.Empty;

    [JsonPropertyName("cargoTemperatureMode")]
    public string CargoTemperatureMode { get; set; } = string.Empty;

    [JsonPropertyName("cargoHumidityAndVentilation")]
    public string CargoHumidityAndVentilation { get; set; } = string.Empty;

    [JsonPropertyName("cargoHandlingRequirements")]
    public string CargoHandlingRequirements { get; set; } = string.Empty;

    [JsonPropertyName("cargoSafetyClass")]
    public string CargoSafetyClass { get; set; } = string.Empty;

    [JsonPropertyName("cargoRequiresSeal")]
    public bool CargoRequiresSeal { get; set; }

    [JsonPropertyName("cargoIconGlyph")]
    public string CargoIconGlyph { get; set; } = "📦";

    [JsonPropertyName("weightKg")]
    public double WeightKg { get; set; }

    [JsonPropertyName("volumeM3")]
    public double VolumeM3 { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "Обычный";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(IsDelivered))]
    [JsonPropertyName("status")]
    private string status = "Ожидает";

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("estimatedTime")]
    public string EstimatedTime { get; set; } = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("notes")]
    private string notes = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("updatedAt")]
    private DateTime updatedAt = DateTime.UtcNow;

    [ObservableProperty]
    [JsonPropertyName("isSyncPending")]
    private bool isSyncPending;

    // UI helper properties
    [JsonIgnore]
    public string StatusColor => Status switch
    {
        "Доставлен" => "#10B981", // Изумрудный
        "Прибыл" => "#06B6D4",    // Бирюзовый (после отметки прибытия)
        "В пути" => "#3B82F6",    // Синий
        "Отказ" => "#EF4444",     // Красный
        _ => "#F59E0B"            // Янтарный / Ожидает
    };

    [JsonIgnore]
    public string PriorityColor => Priority switch
    {
        "Срочно" => "#DC2626",
        "Высокий" => "#EA580C",
        _ => "#6B7280"
    };

    [JsonIgnore]
    public bool IsDelivered => Status == "Доставлен";

    // Вариант 7: Вспомогательные свойства отображения данных справочника правил транспортировки
    [JsonIgnore]
    public string CargoBadgeText => !string.IsNullOrEmpty(CargoCategoryName) 
        ? $"{CargoIconGlyph} {CargoCategoryName}" 
        : "📦 Стандартный груз";

    [JsonIgnore]
    public string CargoTemperatureBadge => !string.IsNullOrEmpty(CargoTemperatureMode) 
        ? $"🌡️ {CargoTemperatureMode}" 
        : "🌡️ Не регламентирован";

    [JsonIgnore]
    public string CargoRegulationSummary => !string.IsNullOrEmpty(CargoHandlingRequirements) 
        ? CargoHandlingRequirements 
        : "Специальных ограничений не установлено";

    [JsonIgnore]
    public bool HasSpecialTemperature => !string.IsNullOrEmpty(CargoTemperatureMode) && !CargoTemperatureMode.Contains("Не регламентирован");
}
