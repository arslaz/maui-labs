namespace laba8;

public partial class StatsPage : ContentPage
{
    public StatsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var books = LibraryService.Books;
        TotalCountLabel.Text = $"Общее количество книг: {books.Count}";

        var rated = books.Where(b => b.Rating > 0).ToList();
        AverageRatingLabel.Text = rated.Count > 0
            ? $"Средняя оценка: {rated.Average(b => b.Rating):F1} / 5"
            : "Средняя оценка: нет оценок";

        GenresCollectionView.ItemsSource = books
            .GroupBy(b => string.IsNullOrEmpty(b.Genre) ? "Другое" : b.Genre)
            .Select(g => $"{g.Key}: {g.Count()} шт.")
            .ToList();
    }
}
