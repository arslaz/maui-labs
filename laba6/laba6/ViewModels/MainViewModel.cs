using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using laba6.Models;

namespace laba6.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _newTitle = string.Empty;
    private string _newAuthor = string.Empty;
    private string _newYear = DateTime.Now.Year.ToString();
    private string _newRating = "4.5";

    public ObservableCollection<Book> Books { get; } = new();

    public int TotalBooksCount => Books.Count;

    public int ReadBooksCount => Books.Count(b => b.IsRead);

    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (_newTitle != value)
            {
                _newTitle = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewAuthor
    {
        get => _newAuthor;
        set
        {
            if (_newAuthor != value)
            {
                _newAuthor = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewYear
    {
        get => _newYear;
        set
        {
            if (_newYear != value)
            {
                _newYear = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewRating
    {
        get => _newRating;
        set
        {
            if (_newRating != value)
            {
                _newRating = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand AddBookCommand { get; }
    public ICommand RemoveBookCommand { get; }
    public ICommand ClearBooksCommand { get; }

    public MainViewModel()
    {
        Books.CollectionChanged += OnBooksCollectionChanged;

        AddBookCommand = new Command(AddBook);
        RemoveBookCommand = new Command<Book>(RemoveBook);
        ClearBooksCommand = new Command(ClearBooks);

        // Начальные данные для наглядной демонстрации конверторов и триггеров
        AddSampleBooks();
    }

    private void AddSampleBooks()
    {
        Books.Add(new Book
        {
            Title = "Мастер и Маргарита",
            Author = "М. А. Булгаков",
            Year = 1967,
            Rating = 4.8,
            IsRead = true
        });

        Books.Add(new Book
        {
            Title = "1984",
            Author = "Джордж Оруэлл",
            Year = 1949,
            Rating = 3.8,
            IsRead = false
        });

        Books.Add(new Book
        {
            Title = "Приключения Незнайки",
            Author = "Н. Н. Носов",
            Year = 1954,
            Rating = 2.5,
            IsRead = false
        });
    }

    private void AddBook()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
            return;

        int.TryParse(NewYear, out int year);
        if (year <= 0)
            year = DateTime.Now.Year;

        double.TryParse(NewRating.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double rating);
        if (rating < 0) rating = 0;
        if (rating > 5) rating = 5;

        var book = new Book
        {
            Title = NewTitle.Trim(),
            Author = string.IsNullOrWhiteSpace(NewAuthor) ? "Неизвестный автор" : NewAuthor.Trim(),
            Year = year,
            Rating = Math.Round(rating, 1),
            IsRead = false
        };

        Books.Add(book);

        // Сброс полей ввода
        NewTitle = string.Empty;
        NewAuthor = string.Empty;
        NewYear = DateTime.Now.Year.ToString();
        NewRating = "4.5";
    }

    private void RemoveBook(Book? book)
    {
        if (book != null)
        {
            Books.Remove(book);
        }
    }

    private void ClearBooks()
    {
        Books.Clear();
    }

    private void OnBooksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Book item in e.NewItems)
            {
                item.PropertyChanged += OnBookPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (Book item in e.OldItems)
            {
                item.PropertyChanged -= OnBookPropertyChanged;
            }
        }

        NotifyCounters();
    }

    private void OnBookPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Book.IsRead))
        {
            OnPropertyChanged(nameof(ReadBooksCount));
        }
    }

    private void NotifyCounters()
    {
        OnPropertyChanged(nameof(TotalBooksCount));
        OnPropertyChanged(nameof(ReadBooksCount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
