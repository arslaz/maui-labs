using System.Collections.ObjectModel;
using laba67.Models;

namespace laba67.Services;

public static class BookmarkStore
{
    public static ObservableCollection<Bookmark> Bookmarks { get; } = new()
    {
        new Bookmark
        {
            BookId = 1,
            BookTitle = "Мастер и Маргарита",
            Note = "Появление клетчатого гражданина на Патриарших",
            PageNumber = 2,
            CreatedAt = DateTime.Now.AddHours(-2)
        }
    };
}
