using System.Text.Json;

namespace laba8;

public static class LibraryService
{
    public static List<Book> Books { get; set; } = new();

    public static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "books.json");
    public static string ExportPath => Path.Combine(FileSystem.AppDataDirectory, "library_export.txt");

    public static async Task LoadBooksAsync()
    {
        if (File.Exists(FilePath))
        {
            string json = await File.ReadAllTextAsync(FilePath);
            Books = JsonSerializer.Deserialize<List<Book>>(json) ?? new();

            foreach (var b in Books)
            {
                if (b.IsPrivate)
                {
                    try
                    {
                        b.Review = await SecureStorage.Default.GetAsync(b.Id) ?? string.Empty;
                    }
                    catch
                    {
                    }
                }
            }
        }
        else
        {
            Books = new List<Book>
            {
                new() { Title = "1984", Author = "Дж. Оруэлл", Year = 1949, Genre = "Фантастика", PageCount = 320, Rating = 5, Review = "Секретные мысли", IsPrivate = true },
                new() { Title = "Мастер и Маргарита", Author = "М. Булгаков", Year = 1967, Genre = "Классика", PageCount = 480, Rating = 5, Review = "Шедевр", IsPrivate = false }
            };
            await SaveBooksAsync();
        }
    }

    public static async Task SaveBooksAsync()
    {
        foreach (var b in Books)
        {
            try
            {
                if (b.IsPrivate)
                {
                    await SecureStorage.Default.SetAsync(b.Id, b.Review);
                }
                else
                {
                    SecureStorage.Default.Remove(b.Id);
                }
            }
            catch
            {
            }
        }

        var saveList = Books.Select(b => new Book
        {
            Id = b.Id,
            Title = b.Title,
            Author = b.Author,
            Year = b.Year,
            Genre = b.Genre,
            PageCount = b.PageCount,
            Rating = b.Rating,
            IsPrivate = b.IsPrivate,
            Review = b.IsPrivate ? string.Empty : b.Review
        }).ToList();

        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(saveList));
    }

    public static async Task DeleteBookAsync(Book book)
    {
        Books.Remove(book);
        try
        {
            SecureStorage.Default.Remove(book.Id);
        }
        catch
        {
        }
        await SaveBooksAsync();
    }

    public static async Task ExportAsync()
    {
        await File.WriteAllTextAsync(ExportPath, JsonSerializer.Serialize(Books));
    }

    public static async Task ImportAsync()
    {
        if (File.Exists(ExportPath))
        {
            string json = await File.ReadAllTextAsync(ExportPath);
            Books = JsonSerializer.Deserialize<List<Book>>(json) ?? new();
            await SaveBooksAsync();
        }
    }

    public static void ResetAll()
    {
        Books.Clear();
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
        Preferences.Default.Clear();
        try
        {
            SecureStorage.Default.RemoveAll();
        }
        catch
        {
        }
    }
}
