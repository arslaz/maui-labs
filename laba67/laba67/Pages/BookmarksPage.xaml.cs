using System.Collections.ObjectModel;
using laba67.Models;
using laba67.Services;

namespace laba67.Pages;

public partial class BookmarksPage : ContentPage
{
    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public BookmarksPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Загрузка сохранённых закладок из хранилища
        Bookmarks.Clear();
        foreach (var bookmark in BookmarkStore.Bookmarks)
        {
            Bookmarks.Add(bookmark);
        }

        // Подписка на сообщения о добавлении новых закладок через MessagingCenter
        MessagingCenter.Subscribe<ReadingPage, Bookmark>(this, "BookmarkAdded", (sender, bookmark) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Bookmarks.Contains(bookmark))
                {
                    Bookmarks.Insert(0, bookmark);
                }
            });
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Корректное освобождение ресурсов при отписке от сообщений
        MessagingCenter.Unsubscribe<ReadingPage, Bookmark>(this, "BookmarkAdded");
    }

    private async void OnOpenBookmarkClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is Bookmark bookmark)
        {
            var book = BookStore.GetBookById(bookmark.BookId) ?? BookStore.GetBookByTitle(bookmark.BookTitle);
            if (book != null)
            {
                // Переход на страницу чтения с открытием конкретной страницы закладки
                await Navigation.PushAsync(new ReadingPage(book, bookmark.PageNumber));
            }
        }
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        // Возврат по стеку навигации (PopAsync)
        await Navigation.PopAsync();
    }
}
