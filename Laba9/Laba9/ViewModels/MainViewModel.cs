using System.Collections.ObjectModel;
using System.Windows.Input;
using Laba9.Models;
using Laba9.Services;

namespace Laba9.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IDiaryStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private List<DiaryEntry> _allEntries = new();

    private string _searchText = string.Empty;
    private string _selectedMoodFilter = "Все настроения";
    private bool _onlyFavorites;
    private bool _isEmpty;
    private double _fontSize;
    private string _statisticsText = string.Empty;

    public ObservableCollection<DiaryEntry> Entries { get; } = new();
    public List<string> MoodFilters => MoodHelper.FilterMoods;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedMoodFilter
    {
        get => _selectedMoodFilter;
        set
        {
            if (SetProperty(ref _selectedMoodFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool OnlyFavorites
    {
        get => _onlyFavorites;
        set
        {
            if (SetProperty(ref _onlyFavorites, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetProperty(ref _isEmpty, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public string StatisticsText
    {
        get => _statisticsText;
        set => SetProperty(ref _statisticsText, value);
    }

    // Commands
    public ICommand LoadEntriesCommand { get; }
    public ICommand AddEntryCommand { get; }
    public ICommand OpenEntryCommand { get; }
    public ICommand EditEntryCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ShareEntryCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand RefreshCommand { get; }

    public MainViewModel(IDiaryStorageService storageService, ISettingsService settingsService)
    {
        _storageService = storageService;
        _settingsService = settingsService;
        Title = "Мой Дневник";
        FontSize = _settingsService.FontSize;

        _settingsService.SettingsChanged += OnSettingsChanged;

        LoadEntriesCommand = new Command(async () => await LoadEntriesAsync());
        AddEntryCommand = new Command(async () => await OnAddEntryAsync());
        OpenEntryCommand = new Command<DiaryEntry>(async (e) => await OnOpenEntryAsync(e));
        EditEntryCommand = new Command<DiaryEntry>(async (e) => await OnEditEntryAsync(e));
        DeleteEntryCommand = new Command<DiaryEntry>(async (e) => await OnDeleteEntryAsync(e));
        ToggleFavoriteCommand = new Command<DiaryEntry>(async (e) => await OnToggleFavoriteAsync(e));
        ShareEntryCommand = new Command<DiaryEntry>(async (e) => await OnShareEntryAsync(e));
        OpenSettingsCommand = new Command(async () => await OnOpenSettingsAsync());
        SearchCommand = new Command(ApplyFilters);
        ClearFilterCommand = new Command(OnClearFilter);
        RefreshCommand = new Command(async () => await LoadEntriesAsync());
    }

    private void OnSettingsChanged()
    {
        FontSize = _settingsService.FontSize;
        ApplyFilters();
    }

    public async Task LoadEntriesAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            _allEntries = await _storageService.LoadEntriesAsync();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            await DialogService.ShowAlertAsync("Ошибка загрузки", $"Не удалось загрузить записи: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyFilters()
    {
        IEnumerable<DiaryEntry> filtered = _allEntries;

        // Фильтр по избранному
        if (OnlyFavorites)
        {
            filtered = filtered.Where(e => e.IsFavorite);
        }

        // Фильтр по настроению
        if (!string.IsNullOrWhiteSpace(SelectedMoodFilter) && SelectedMoodFilter != "Все настроения")
        {
            filtered = filtered.Where(e => e.Mood == SelectedMoodFilter);
        }

        // Поиск по тексту, заголовку и тегам
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(e =>
                (e.Title != null && e.Title.ToLowerInvariant().Contains(query)) ||
                (e.Content != null && !e.IsConfidential && e.Content.ToLowerInvariant().Contains(query)) ||
                (e.Tags != null && e.Tags.Any(t => t.ToLowerInvariant().Contains(query.TrimStart('#'))))
            );
        }

        // Сортировка по умолчанию из настроек
        filtered = _settingsService.DefaultSort switch
        {
            SettingsService.SortDateAsc => filtered.OrderBy(e => e.CreatedAt),
            SettingsService.SortTitleAsc => filtered.OrderBy(e => e.Title),
            SettingsService.SortUpdatedDesc => filtered.OrderByDescending(e => e.UpdatedAt),
            _ => filtered.OrderByDescending(e => e.CreatedAt)
        };

        Entries.Clear();
        foreach (var entry in filtered)
        {
            Entries.Add(entry);
        }

        IsEmpty = Entries.Count == 0;
        var favCount = _allEntries.Count(e => e.IsFavorite);
        StatisticsText = $"Всего записей: {_allEntries.Count} | Избранных: {favCount}";
    }

    private void OnClearFilter()
    {
        SearchText = string.Empty;
        SelectedMoodFilter = "Все настроения";
        OnlyFavorites = false;
    }

    private async Task OnAddEntryAsync()
    {
        await Shell.Current.GoToAsync("entrydetail?id=new");
    }

    private async Task OnOpenEntryAsync(DiaryEntry? entry)
    {
        if (entry == null) return;
        await Shell.Current.GoToAsync($"entrydetail?id={entry.Id}");
    }

    private async Task OnEditEntryAsync(DiaryEntry? entry)
    {
        if (entry == null) return;
        await Shell.Current.GoToAsync($"entrydetail?id={entry.Id}&mode=edit");
    }

    private async Task OnDeleteEntryAsync(DiaryEntry? entry)
    {
        if (entry == null) return;

        // Защита от случайного удаления
        bool confirm = await DialogService.ShowConfirmAsync(
            "Удаление записи",
            $"Вы уверены, что хотите удалить запись «{entry.Title}»?",
            "Удалить",
            "Отмена");

        if (!confirm) return;

        _allEntries.Remove(entry);
        await _storageService.SaveEntriesAsync(_allEntries);
        ApplyFilters();
    }

    private async Task OnToggleFavoriteAsync(DiaryEntry? entry)
    {
        if (entry == null) return;

        entry.IsFavorite = !entry.IsFavorite;
        await _storageService.SaveEntriesAsync(_allEntries);
        ApplyFilters();
    }

    private async Task OnShareEntryAsync(DiaryEntry? entry)
    {
        if (entry == null) return;

        try
        {
            var contentToShare = entry.IsConfidential
                ? $"Запись из дневника: {entry.Title}\nДата: {entry.FormattedDate}\n(Текст конфиденциален)"
                : $"Запись из дневника: {entry.Title}\nДата: {entry.FormattedDate}\nНастроение: {entry.Mood}\n\n{entry.Content}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = entry.Title,
                Text = contentToShare
            });
        }
        catch (Exception ex)
        {
            await DialogService.ShowAlertAsync("Поделиться", $"Не удалось отправить запись: {ex.Message}");
        }
    }

    private async Task OnOpenSettingsAsync()
    {
        await Shell.Current.GoToAsync("settings");
    }
}
