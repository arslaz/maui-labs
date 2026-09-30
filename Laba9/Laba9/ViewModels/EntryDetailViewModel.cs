using System.Windows.Input;
using Laba9.Models;
using Laba9.Services;

namespace Laba9.ViewModels;

public class EntryDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IDiaryStorageService _storageService;
    private readonly ISettingsService _settingsService;

    private string _entryId = string.Empty;
    private string _titleText = string.Empty;
    private string _contentText = string.Empty;
    private DateTime _entryDate = DateTime.Today;
    private string _selectedMood = "😊 Отличное";
    private string _tagsText = string.Empty;
    private bool _isFavorite;
    private bool _isConfidential;
    private bool _isNew = true;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private double _fontSize;
    private string _statusInfo = string.Empty;
    private DiaryEntry? _currentEntry;

    public string TitleText
    {
        get => _titleText;
        set
        {
            if (SetProperty(ref _titleText, value))
                ClearError();
        }
    }

    public string ContentText
    {
        get => _contentText;
        set
        {
            if (SetProperty(ref _contentText, value))
                ClearError();
        }
    }

    public DateTime EntryDate
    {
        get => _entryDate;
        set
        {
            if (SetProperty(ref _entryDate, value))
            {
                // Защита от ввода даты в будущем при изменении пользователем
                if (value.Date > DateTime.Today)
                {
                    SetError("⚠️ Дата записи не может быть в будущем!");
                }
                else
                {
                    ClearError();
                }
            }
        }
    }

    public DateTime MaxDate => DateTime.Today;

    public string SelectedMood
    {
        get => _selectedMood;
        set => SetProperty(ref _selectedMood, value);
    }

    public List<string> AvailableMoods => MoodHelper.AllMoods;

    public string TagsText
    {
        get => _tagsText;
        set => SetProperty(ref _tagsText, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsConfidential
    {
        get => _isConfidential;
        set => SetProperty(ref _isConfidential, value);
    }

    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public string StatusInfo
    {
        get => _statusInfo;
        set => SetProperty(ref _statusInfo, value);
    }

    // Commands
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShareCommand { get; }

    public EntryDetailViewModel(IDiaryStorageService storageService, ISettingsService settingsService)
    {
        _storageService = storageService;
        _settingsService = settingsService;
        FontSize = _settingsService.FontSize;

        SaveCommand = new Command(async () => await OnSaveAsync());
        CancelCommand = new Command(async () => await OnCancelAsync());
        DeleteCommand = new Command(async () => await OnDeleteAsync());
        ShareCommand = new Command(async () => await OnShareAsync());
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string id)
        {
            _entryId = id;
            if (_entryId == "new")
            {
                IsNew = true;
                Title = "Новая запись";
                TitleText = string.Empty;
                ContentText = string.Empty;
                EntryDate = DateTime.Today;
                SelectedMood = "😊 Отличное";
                TagsText = string.Empty;
                IsFavorite = false;
                IsConfidential = false;
                StatusInfo = "Заполните поля и сохраните запись";
                ClearError();
            }
            else
            {
                await LoadEntryDetailsAsync(_entryId);
            }
        }
    }

    private async Task LoadEntryDetailsAsync(string id)
    {
        try
        {
            IsBusy = true;
            var entries = await _storageService.LoadEntriesAsync();
            _currentEntry = entries.FirstOrDefault(e => e.Id == id);

            if (_currentEntry != null)
            {
                IsNew = false;
                Title = "Запись дневника";
                TitleText = _currentEntry.Title;
                ContentText = _currentEntry.Content;
                EntryDate = _currentEntry.CreatedAt;
                SelectedMood = _currentEntry.Mood;
                TagsText = _currentEntry.Tags != null ? string.Join(", ", _currentEntry.Tags) : string.Empty;
                IsFavorite = _currentEntry.IsFavorite;
                IsConfidential = _currentEntry.IsConfidential;
                StatusInfo = $"Создано: {_currentEntry.FormattedDate} | Обновлено: {_currentEntry.UpdatedAt:dd.MM.yyyy HH:mm}";
            }
            else
            {
                SetError("Запись не найдена!");
                await DialogService.ShowAlertAsync("Запись не найдена", "Запрашиваемая запись не найдена или была удалена.");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            SetError($"Ошибка при загрузке: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnSaveAsync()
    {
        // 1. Защита от ввода даты в будущем
        if (EntryDate.Date > DateTime.Today)
        {
            SetError("Дата записи не может быть в будущем! Выберите сегодняшнюю или прошедшую дату.");
            return;
        }

        // 2. Валидация заголовка
        if (string.IsNullOrWhiteSpace(TitleText))
        {
            SetError("Заголовок записи не может быть пустым.");
            return;
        }

        // 3. Валидация содержимого
        if (string.IsNullOrWhiteSpace(ContentText))
        {
            SetError("Текст записи не может быть пустым.");
            return;
        }

        try
        {
            IsBusy = true;
            var entries = await _storageService.LoadEntriesAsync();

            // Парсим теги: удаляем пробелы, #, дубликаты
            var parsedTags = (TagsText ?? string.Empty)
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().TrimStart('#'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (IsNew)
            {
                var newEntry = new DiaryEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = TitleText.Trim(),
                    Content = ContentText.Trim(),
                    CreatedAt = EntryDate.Date == DateTime.Today ? DateTime.Now : EntryDate.Date,
                    UpdatedAt = DateTime.Now,
                    Mood = SelectedMood,
                    Tags = parsedTags,
                    IsFavorite = IsFavorite,
                    IsConfidential = IsConfidential
                };

                entries.Insert(0, newEntry);
            }
            else if (_currentEntry != null)
            {
                var target = entries.FirstOrDefault(e => e.Id == _currentEntry.Id);
                if (target != null)
                {
                    target.Title = TitleText.Trim();
                    target.Content = ContentText.Trim();
                    target.CreatedAt = EntryDate;
                    target.UpdatedAt = DateTime.Now;
                    target.Mood = SelectedMood;
                    target.Tags = parsedTags;
                    target.IsFavorite = IsFavorite;
                    target.IsConfidential = IsConfidential;
                }
            }

            await _storageService.SaveEntriesAsync(entries);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetError($"Не удалось сохранить запись: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnCancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private async Task OnDeleteAsync()
    {
        if (IsNew || _currentEntry == null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        bool confirm = await DialogService.ShowConfirmAsync(
            "Удаление записи",
            $"Вы действительно хотите удалить запись «{TitleText}»?",
            "Удалить",
            "Отмена");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            var entries = await _storageService.LoadEntriesAsync();
            entries.RemoveAll(e => e.Id == _currentEntry.Id);
            await _storageService.SaveEntriesAsync(entries);

            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnShareAsync()
    {
        if (string.IsNullOrWhiteSpace(TitleText)) return;

        try
        {
            var contentToShare = IsConfidential
                ? $"Запись из дневника: {TitleText}\nДата: {EntryDate:dd.MM.yyyy}\n(Текст конфиденциален)"
                : $"Запись из дневника: {TitleText}\nДата: {EntryDate:dd.MM.yyyy}\nНастроение: {SelectedMood}\n\n{ContentText}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = TitleText,
                Text = contentToShare
            });
        }
        catch (Exception ex)
        {
            SetError($"Ошибка при отправке: {ex.Message}");
        }
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
