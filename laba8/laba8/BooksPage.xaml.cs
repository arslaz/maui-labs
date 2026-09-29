namespace laba8;

public partial class BooksPage : ContentPage
{
    public BooksPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LibraryService.LoadBooksAsync();
        ApplyFilter();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string text = SearchBarControl.Text?.Trim().ToLower() ?? string.Empty;
        string genre = GenrePicker.SelectedItem?.ToString() ?? "Все жанры";
        bool onlyRated = Preferences.Default.Get("only_rated", false);
        string sort = Preferences.Default.Get("sort", "По автору");

        var items = LibraryService.Books.AsEnumerable();

        if (onlyRated)
        {
            items = items.Where(b => b.Rating > 0);
        }

        if (genre != "Все жанры")
        {
            items = items.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrEmpty(text))
        {
            items = items.Where(b => b.Title.ToLower().Contains(text) || b.Author.ToLower().Contains(text));
        }

        items = sort switch
        {
            "По автору" => items.OrderBy(b => b.Author),
            "По году" => items.OrderBy(b => b.Year),
            "По оценке" => items.OrderByDescending(b => b.Rating),
            _ => items
        };

        BooksCollection.ItemsSource = items.ToList();
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new BookEditPage());
    }

    private async void OnBookSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Book book)
        {
            BooksCollection.SelectedItem = null;
            await Navigation.PushAsync(new BookEditPage(book));
        }
    }
}
