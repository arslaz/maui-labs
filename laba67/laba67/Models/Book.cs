using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace laba67.Models;

public class Book : INotifyPropertyChanged
{
    private int _currentPage = 1;

    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;

    // Список страниц книги для многостраничного чтения
    public List<string> Pages { get; set; } = new();

    public int TotalPages => Pages.Count > 0 ? Pages.Count : 1;

    // Текущая страница, на которой остановился читатель
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            int clamped = Math.Clamp(value, 1, TotalPages);
            if (_currentPage != clamped)
            {
                _currentPage = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPageText));
                OnPropertyChanged(nameof(CurrentPageContent));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(ProgressPercentText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    // Текст текущей открытой страницы
    public string CurrentPageContent =>
        (Pages.Count >= CurrentPage && CurrentPage > 0)
            ? Pages[CurrentPage - 1]
            : "Страница пуста.";

    // Строка отображения номера страницы
    public string CurrentPageText => $"Страница {CurrentPage} из {TotalPages}";

    // Прогресс чтения рассчитывается строго на основе текущей страницы
    public double Progress => TotalPages > 0 ? (double)CurrentPage / TotalPages : 0.0;

    public string ProgressPercentText => $"{(int)(Progress * 100)}%";

    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
