namespace laba67.Models;

public class Bookmark
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string FormattedDate => CreatedAt.ToString("dd.MM.yyyy HH:mm");
}
