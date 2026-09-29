using laba67.Models;
using laba67.Services;

namespace laba67.Pages;

public partial class ReadingPage : ContentPage
{
    public Book CurrentBook { get; set; }

    public ReadingPage(Book book, int? targetPage = null)
    {
        InitializeComponent();

        // Прием переданных данных книги при навигации
        CurrentBook = book;

        // Если передан номер страницы (например, из закладки), открываем её,
        // иначе открываем ту страницу, на которой читатель остановился ранее
        if (targetPage.HasValue)
        {
            CurrentBook.CurrentPage = targetPage.Value;
        }

        BindingContext = this;
    }

    private void OnPreviousPageClicked(object? sender, EventArgs e)
    {
        if (CurrentBook.CanGoPrevious)
        {
            CurrentBook.CurrentPage--;
            SyncProgress();
        }
    }

    private void OnNextPageClicked(object? sender, EventArgs e)
    {
        if (CurrentBook.CanGoNext)
        {
            CurrentBook.CurrentPage++;
            SyncProgress();
        }
    }

    private void OnSliderPageChanged(object? sender, ValueChangedEventArgs e)
    {
        if (CurrentBook != null)
        {
            int page = (int)Math.Round(e.NewValue);
            if (page != CurrentBook.CurrentPage)
            {
                CurrentBook.CurrentPage = page;
                SyncProgress();
            }
        }
    }

    private async void OnFinishBookClicked(object? sender, EventArgs e)
    {
        CurrentBook.CurrentPage = CurrentBook.TotalPages;
        SyncProgress();

        await DisplayAlertAsync("Книга завершена",
            $"Вы перешли на последнюю страницу ({CurrentBook.TotalPages}). Прогресс 100% передан в каталог через MessagingCenter!",
            "OK");
    }

    private async void OnAddBookmarkClicked(object? sender, EventArgs e)
    {
        var note = string.IsNullOrWhiteSpace(NoteEntry.Text)
            ? $"Закладка на стр. {CurrentBook.CurrentPage}"
            : NoteEntry.Text.Trim();

        var bookmark = new Bookmark
        {
            BookId = CurrentBook.Id,
            BookTitle = CurrentBook.Title,
            PageNumber = CurrentBook.CurrentPage, // Закладка привязана к конкретной странице
            Note = note,
            CreatedAt = DateTime.Now
        };

        // Сохраняем в список закладок
        BookmarkStore.Bookmarks.Insert(0, bookmark);

        // Отправка сообщения о закладке через MessagingCenter
        MessagingCenter.Send(this, "BookmarkAdded", bookmark);

        NoteEntry.Text = string.Empty;

        await DisplayAlertAsync("Закладка добавлена",
            $"Закладка для книги «{bookmark.BookTitle}» на странице {bookmark.PageNumber} успешно создана и передана через MessagingCenter!",
            "OK");
    }

    private void SyncProgress()
    {
        // Отправка сообщения через MessagingCenter с обновленным прогрессом и страницей
        MessagingCenter.Send(this, "BookProgressUpdated", CurrentBook);
    }

    private async void OnOpenBookmarksClicked(object? sender, EventArgs e)
    {
        // Переход на страницу закладок по стеку навигации
        await Navigation.PushAsync(new BookmarksPage());
    }

    private async void OnBackToCatalogClicked(object? sender, EventArgs e)
    {
        SyncProgress();
        // Возврат на предыдущую страницу в стеке навигации
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // При закрытии страницы или переходе всегда сохраняем страницу и прогресс
        SyncProgress();
    }
}
