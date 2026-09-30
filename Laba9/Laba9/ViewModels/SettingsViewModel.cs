using System.Windows.Input;
using Laba9.Services;

namespace Laba9.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IDiaryStorageService _storageService;

    private double _fontSize;
    private string _theme = "Системная";
    private string _defaultSort = SettingsService.SortDateDesc;
    private string _statusMessage = string.Empty;
    private int _entriesCount;

    public List<double> AvailableFontSizes => SettingsService.AvailableFontSizes;
    public List<string> AvailableThemes => SettingsService.AvailableThemes;
    public List<string> AvailableSortOptions => SettingsService.AvailableSortOptions;

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, value))
            {
                _settingsService.FontSize = value;
                StatusMessage = $"Размер шрифта изменен на {value:F0} pt";
            }
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                _settingsService.Theme = value;
                StatusMessage = $"Тема оформления изменена на «{value}»";
            }
        }
    }

    public string DefaultSort
    {
        get => _defaultSort;
        set
        {
            if (SetProperty(ref _defaultSort, value))
            {
                _settingsService.DefaultSort = value;
                StatusMessage = "Сортировка по умолчанию обновлена";
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public int EntriesCount
    {
        get => _entriesCount;
        set => SetProperty(ref _entriesCount, value);
    }

    public string StoragePath => _storageService.StorageFilePath;

    // Commands
    public ICommand BackupDataCommand { get; }
    public ICommand RestoreDataCommand { get; }
    public ICommand ResetAllDataCommand { get; }
    public ICommand CreateSampleDataCommand { get; }
    public ICommand GoBackCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IDiaryStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
        Title = "Настройки дневника";

        _fontSize = _settingsService.FontSize;
        _theme = _settingsService.Theme;
        _defaultSort = _settingsService.DefaultSort;

        BackupDataCommand = new Command(async () => await OnBackupDataAsync());
        RestoreDataCommand = new Command(async () => await OnRestoreDataAsync());
        ResetAllDataCommand = new Command(async () => await OnResetAllDataAsync());
        CreateSampleDataCommand = new Command(async () => await OnCreateSampleDataAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));

        _ = RefreshEntriesCountAsync();
    }

    public async Task RefreshEntriesCountAsync()
    {
        try
        {
            var entries = await _storageService.LoadEntriesAsync();
            EntriesCount = entries.Count;
        }
        catch
        {
            EntriesCount = 0;
        }
    }

    private async Task OnBackupDataAsync()
    {
        try
        {
            IsBusy = true;
            var backupPath = await _storageService.ExportBackupAsync();
            StatusMessage = "Резервная копия успешно создана!";
            await DialogService.ShowAlertAsync("Резервное копирование", $"Резервная копия записей сохранена в файл:\n{backupPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка резервного копирования: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnRestoreDataAsync()
    {
        bool confirm = await DialogService.ShowConfirmAsync(
            "Восстановление данных",
            "Вы действительно хотите восстановить записи из последней резервной копии? Текущие записи будут заменены.",
            "Восстановить",
            "Отмена");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            bool success = await _storageService.RestoreBackupAsync();

            if (success)
            {
                await RefreshEntriesCountAsync();
                StatusMessage = "Записи успешно восстановлены из резервной копии!";
                await DialogService.ShowAlertAsync("Успех", "Данные успешно восстановлены!");
            }
            else
            {
                StatusMessage = "Резервная копия не найдена. Сначала создайте её.";
                await DialogService.ShowAlertAsync("Ошибка", "Файл резервной копии не найден.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка восстановления: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnResetAllDataAsync()
    {
        // Защита от случайного сброса
        bool confirm = await DialogService.ShowConfirmAsync(
            "⚠️ СБРОС ВСЕХ ДАННЫХ",
            "Внимание! Это действие удалит ВСЕ записи дневника и сбросит все настройки на стандартные. Действие необратимо!\n\nВы уверены?",
            "Да, сбросить всё",
            "Отмена");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            await _storageService.ResetAllDataAsync();
            _settingsService.ResetSettings();

            FontSize = _settingsService.FontSize;
            Theme = _settingsService.Theme;
            DefaultSort = _settingsService.DefaultSort;
            EntriesCount = 0;

            StatusMessage = "Все данные и настройки успешно сброшены.";
            await DialogService.ShowAlertAsync("Сброс выполнен", "Все данные и настройки очищены.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при сбросе данных: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnCreateSampleDataAsync()
    {
        try
        {
            IsBusy = true;
            await _storageService.CreateSampleDataAsync();
            await RefreshEntriesCountAsync();
            StatusMessage = "Примеры записей успешно добавлены!";
            await DialogService.ShowAlertAsync("Успех", "Демонстрационные записи созданы.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
