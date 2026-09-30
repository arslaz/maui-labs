using System.Text.Json.Serialization;

namespace Laba9.Models;

public class DiaryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string Mood { get; set; } = "😊 Отличное";
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
    public bool IsConfidential { get; set; }
    public bool IsEncrypted { get; set; }

    [JsonIgnore]
    public string FormattedDate => CreatedAt.ToString("dd.MM.yyyy HH:mm");

    [JsonIgnore]
    public string FormattedShortDate => CreatedAt.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));

    [JsonIgnore]
    public string DisplaySnippet
    {
        get
        {
            if (IsConfidential)
                return "🔒 Конфиденциальное содержимое (зашифровано)";

            if (string.IsNullOrWhiteSpace(Content))
                return "Нет описания";

            var clean = Content.Replace("\r\n", " ").Replace("\n", " ");
            return clean.Length > 85 ? clean.Substring(0, 85) + "..." : clean;
        }
    }

    [JsonIgnore]
    public string TagsDisplay => Tags is { Count: > 0 } 
        ? string.Join("  ", Tags.Select(t => t.StartsWith("#") ? t : $"#{t}"))
        : string.Empty;

    [JsonIgnore]
    public bool HasTags => Tags is { Count: > 0 };
}
