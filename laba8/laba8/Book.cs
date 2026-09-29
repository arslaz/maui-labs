namespace laba8;

public class Book
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int Rating { get; set; }
    public string Review { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }

    public string Info => $"{Author} ({Year} г.) • {Genre} • {PageCount} стр.";
    public string Score => Rating > 0 ? $"{Rating}/5 ★" : "Без оценки";
    public string Status => IsPrivate ? "🔒 Личный отзыв" : "";
}
