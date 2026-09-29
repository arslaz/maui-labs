using System.Collections.ObjectModel;
using System.Windows.Input;
using laba67.Models;
using laba67.Services;

namespace laba67.Pages;

public partial class CatalogPage : ContentPage
{
    public ObservableCollection<Book> Books => BookStore.Books;

    public ICommand OpenBookCommand { get; }

    public CatalogPage()
    {
        InitializeComponent();

        OpenBookCommand = new Command<Book>(async (book) =>
        {
            if (book != null)
            {
                // Переход на страницу чтения с передачей объекта книги (открывается сохраненная страница)
                await Navigation.PushAsync(new ReadingPage(book));
            }
        });

        BindingContext = this;

        // Подписка на обновление прогресса и текущей страницы через MessagingCenter
        MessagingCenter.Subscribe<ReadingPage, Book>(this, "BookProgressUpdated", (sender, updatedBook) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var book = BookStore.GetBookById(updatedBook.Id);
                if (book != null)
                {
                    book.CurrentPage = updatedBook.CurrentPage;
                }
            });
        });
    }

    private async void OnBookmarksClicked(object? sender, EventArgs e)
    {
        // Переход на страницу закладок по стеку навигации
        await Navigation.PushAsync(new BookmarksPage());
    }

    ~CatalogPage()
    {
        // Корректное освобождение ресурсов и отписка от сообщений
        MessagingCenter.Unsubscribe<ReadingPage, Book>(this, "BookProgressUpdated");
    }
}
