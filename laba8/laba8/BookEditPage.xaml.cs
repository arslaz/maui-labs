namespace laba8;

public partial class BookEditPage : ContentPage
{
    private readonly Book? _book;

    public BookEditPage()
    {
        InitializeComponent();
        DeleteButton.IsVisible = false;
    }

    public BookEditPage(Book book)
    {
        InitializeComponent();
        _book = book;
        DeleteButton.IsVisible = true;

        TitleEntry.Text = book.Title;
        AuthorEntry.Text = book.Author;
        YearEntry.Text = book.Year > 0 ? book.Year.ToString() : string.Empty;
        PageCountEntry.Text = book.PageCount > 0 ? book.PageCount.ToString() : string.Empty;
        GenrePicker.SelectedItem = book.Genre;
        RatingPicker.SelectedIndex = book.Rating;
        ReviewEditor.Text = book.Review;
        PrivateSwitch.IsToggled = book.IsPrivate;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
        {
            return;
        }

        int.TryParse(YearEntry.Text, out int year);
        int.TryParse(PageCountEntry.Text, out int pages);

        var book = _book ?? new Book();
        book.Title = TitleEntry.Text.Trim();
        book.Author = AuthorEntry.Text?.Trim() ?? string.Empty;
        book.Year = year;
        book.PageCount = pages;
        book.Genre = GenrePicker.SelectedItem?.ToString() ?? "Классика";
        book.Rating = RatingPicker.SelectedIndex;
        book.Review = ReviewEditor.Text?.Trim() ?? string.Empty;
        book.IsPrivate = PrivateSwitch.IsToggled;

        if (_book == null)
        {
            LibraryService.Books.Add(book);
        }

        await LibraryService.SaveBooksAsync();
        await Navigation.PopAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_book != null)
        {
            await LibraryService.DeleteBookAsync(_book);
            await Navigation.PopAsync();
        }
    }
}
