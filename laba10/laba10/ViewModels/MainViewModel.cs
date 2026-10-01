using System.Collections.ObjectModel;
using System.Windows.Input;
using laba10.Models;
using laba10.Services;

namespace laba10.ViewModels;

/// <summary>
/// ViewModel главной страницы: управление каталогом тестов, фильтрация по уровню (Preferences),
/// контекстные команды (удаление, дублирование, экспорт, сброс) и возобновление замороженного теста.
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly ITestService _testService;
    private readonly ISettingsService _settingsService;

    private ObservableCollection<ExamTest> _tests = new();
    private ObservableCollection<ExamTest> _filteredTests = new();
    private string _selectedLevel = "Все";
    private bool _hasFrozenTest;
    private string _frozenTestInfo = string.Empty;
    private bool _isTeacherMode;
    private string _studentGreeting = string.Empty;

    public ObservableCollection<ExamTest> FilteredTests
    {
        get => _filteredTests;
        set => SetProperty(ref _filteredTests, value);
    }

    public List<string> AvailableLevels { get; } = new() { "Все", "B1", "B2", "C1" };

    public string SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
            {
                _settingsService.TargetLanguageLevel = value;
                ApplyFilter();
            }
        }
    }

    public bool HasFrozenTest
    {
        get => _hasFrozenTest;
        set => SetProperty(ref _hasFrozenTest, value);
    }

    public string FrozenTestInfo
    {
        get => _frozenTestInfo;
        set => SetProperty(ref _frozenTestInfo, value);
    }

    public bool IsTeacherMode
    {
        get => _isTeacherMode;
        set => SetProperty(ref _isTeacherMode, value);
    }

    public string StudentGreeting
    {
        get => _studentGreeting;
        set => SetProperty(ref _studentGreeting, value);
    }

    // Команды
    public ICommand LoadTestsCommand { get; }
    public ICommand StartTestCommand { get; }
    public ICommand ResumeFrozenTestCommand { get; }
    public ICommand DiscardFrozenTestCommand { get; }
    public ICommand DuplicateTestCommand { get; }
    public ICommand DeleteTestCommand { get; }
    public ICommand ExportTestCommand { get; }
    public ICommand ResetProgressCommand { get; }
    public ICommand ShowTestOptionsCommand { get; }
    public ICommand EditTestCommand { get; }
    public ICommand EnterTeacherModeCommand { get; }
    public ICommand CreateNewTestCommand { get; }

    public MainViewModel(ITestService testService, ISettingsService settingsService)
    {
        _testService = testService;
        _settingsService = settingsService;
        Title = "Каталог экзаменов";

        _selectedLevel = _settingsService.TargetLanguageLevel;

        LoadTestsCommand = new Command(async () => await LoadTestsAsync());
        StartTestCommand = new Command<ExamTest>(async (t) => await ExecuteStartTestAsync(t));
        ResumeFrozenTestCommand = new Command(async () => await ExecuteResumeFrozenTestAsync());
        DiscardFrozenTestCommand = new Command(async () => await ExecuteDiscardFrozenTestAsync());
        DuplicateTestCommand = new Command<ExamTest>(async (t) => await ExecuteDuplicateTestAsync(t));
        DeleteTestCommand = new Command<ExamTest>(async (t) => await ExecuteDeleteTestAsync(t));
        ExportTestCommand = new Command<ExamTest>(async (t) => await ExecuteExportTestAsync(t));
        ResetProgressCommand = new Command<ExamTest>(async (t) => await ExecuteResetProgressAsync(t));
        ShowTestOptionsCommand = new Command<ExamTest>(async (t) => await ExecuteShowTestOptionsAsync(t));
        EditTestCommand = new Command<ExamTest>(async (t) => await ExecuteEditTestAsync(t));
        EnterTeacherModeCommand = new Command(async () => await ExecuteEnterTeacherModeAsync());
        CreateNewTestCommand = new Command(async () => await ExecuteCreateNewTestAsync());
    }

    private async Task ExecuteEditTestAsync(ExamTest? test)
    {
        if (test == null) return;
        await Shell.Current.GoToAsync("TeacherEditPage", new Dictionary<string, object>
        {
            ["Test"] = test
        });
    }

    private async Task ExecuteShowTestOptionsAsync(ExamTest? test)
    {
        if (test == null) return;

        var buttons = new List<string>
        {
            "Дублировать тест", 
            "Экспортировать в JSON", 
            "Сбросить прогресс"
        };

        if (IsTeacherMode)
        {
            buttons.Insert(0, "✏️ Редактировать вопросы теста");
        }

        string action = await DisplayActionSheetAsync(
            $"Контекстное меню: {test.Title}", 
            "Отмена", 
            "Удалить тест", 
            buttons.ToArray());

        switch (action)
        {
            case "✏️ Редактировать вопросы теста":
                await ExecuteEditTestAsync(test);
                break;
            case "Дублировать тест":
                await ExecuteDuplicateTestAsync(test);
                break;
            case "Экспортировать в JSON":
                await ExecuteExportTestAsync(test);
                break;
            case "Сбросить прогресс":
                await ExecuteResetProgressAsync(test);
                break;
            case "Удалить тест":
                await ExecuteDeleteTestAsync(test);
                break;
        }
    }

    public async Task InitializeAsync()
    {
        StudentGreeting = $"Кандидат: {_settingsService.StudentName}";
        IsTeacherMode = _settingsService.IsTeacherModeActive;
        await LoadTestsAsync();
        await CheckFrozenTestAsync();
    }

    private async Task LoadTestsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var items = await _testService.GetTestsAsync();
            _tests = new ObservableCollection<ExamTest>(items);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", $"Не удалось загрузить тесты: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(SelectedLevel) || SelectedLevel == "Все")
        {
            FilteredTests = new ObservableCollection<ExamTest>(_tests);
        }
        else
        {
            var filtered = _tests.Where(t => string.Equals(t.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase)).ToList();
            FilteredTests = new ObservableCollection<ExamTest>(filtered);
        }
    }

    private async Task CheckFrozenTestAsync()
    {
        var frozen = await _testService.GetFrozenStateAsync();
        if (frozen != null)
        {
            HasFrozenTest = true;
            FrozenTestInfo = $"Замороженный тест: «{frozen.TestTitle}» (Вопрос {frozen.CurrentQuestionIndex + 1} из {frozen.Questions.Count}, осталось {frozen.RemainingSeconds / 60:D2}:{frozen.RemainingSeconds % 60:D2})";
        }
        else
        {
            HasFrozenTest = false;
            FrozenTestInfo = string.Empty;
        }
    }

    private async Task ExecuteStartTestAsync(ExamTest? test)
    {
        if (test == null) return;

        // Если есть сохраненный замороженный тест этого или другого типа, спросим
        var frozen = await _testService.GetFrozenStateAsync();
        if (frozen != null)
        {
            bool resumeExisting = await DisplayConfirmationAsync("Предупреждение", 
                "У вас есть ранее сохраненный замороженный тест. Возобновить его или начать выбранный заново?", 
                "Возобновить", "Начать заново");
            if (resumeExisting)
            {
                await ExecuteResumeFrozenTestAsync();
                return;
            }
            else
            {
                await _testService.ClearFrozenStateAsync();
                HasFrozenTest = false;
            }
        }

        // Переход на страницу экзамена
        var navParams = new Dictionary<string, object>
        {
            { "TestId", test.Id },
            { "IsResuming", false }
        };
        await Shell.Current.GoToAsync("ExamPage", navParams);
    }

    private async Task ExecuteResumeFrozenTestAsync()
    {
        var frozen = await _testService.GetFrozenStateAsync();
        if (frozen == null)
        {
            await DisplayAlertAsync("Информация", "Нет сохраненных замороженных тестов.");
            HasFrozenTest = false;
            return;
        }

        var navParams = new Dictionary<string, object>
        {
            { "TestId", frozen.TestId },
            { "IsResuming", true }
        };
        await Shell.Current.GoToAsync("ExamPage", navParams);
    }

    private async Task ExecuteDiscardFrozenTestAsync()
    {
        bool confirm = await DisplayConfirmationAsync("Отмена сессии", "Удалить сохраненное состояние замороженного теста?");
        if (confirm)
        {
            await _testService.ClearFrozenStateAsync();
            HasFrozenTest = false;
            FrozenTestInfo = string.Empty;
            await DisplayAlertAsync("Успешно", "Замороженная сессия сброшена.");
        }
    }

    private async Task ExecuteDuplicateTestAsync(ExamTest? test)
    {
        if (test == null) return;

        try
        {
            var duplicated = await _testService.DuplicateTestAsync(test.Id);
            _tests.Add(duplicated);
            ApplyFilter();
            await DisplayAlertAsync("Дублирование", $"Тест «{test.Title}» успешно скопирован.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", ex.Message);
        }
    }

    private async Task ExecuteDeleteTestAsync(ExamTest? test)
    {
        if (test == null) return;

        bool confirm = await DisplayConfirmationAsync("Удаление теста", $"Вы действительно хотите удалить тест «{test.Title}»?");
        if (!confirm) return;

        try
        {
            bool deleted = await _testService.DeleteTestAsync(test.Id);
            if (deleted)
            {
                _tests.Remove(test);
                ApplyFilter();
                await DisplayAlertAsync("Успешно", "Тест удален из каталога.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", ex.Message);
        }
    }

    private async Task ExecuteExportTestAsync(ExamTest? test)
    {
        if (test == null) return;

        try
        {
            var path = await _testService.ExportTestToJsonAsync(test.Id);
            await DisplayAlertAsync("Экспорт завершен", 
                $"Структура теста успешно сохранена в файл:\n{path}\n\nФайл готов к передаче на другие устройства.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка экспорта", ex.Message);
        }
    }

    private async Task ExecuteResetProgressAsync(ExamTest? test)
    {
        if (test == null) return;

        bool confirm = await DisplayConfirmationAsync("Сброс прогресса", $"Сбросить баллы и статус прохождения для «{test.Title}»?");
        if (!confirm) return;

        try
        {
            await _testService.ResetTestProgressAsync(test.Id);
            await LoadTestsAsync();
            await DisplayAlertAsync("Сброс", "Прогресс теста обнулен.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", ex.Message);
        }
    }

    private async Task ExecuteEnterTeacherModeAsync()
    {
        if (IsTeacherMode)
        {
            _settingsService.IsTeacherModeActive = false;
            IsTeacherMode = false;
            await DisplayAlertAsync("Режим преподавателя", "Режим редактирования отключен.");
            return;
        }

        string result = await Shell.Current.DisplayPromptAsync(
            "Авторизация преподавателя",
            "Введите секретный токен доступа (по умолчанию: TEACHER2026):",
            "Войти", "Отмена", "Токен доступа");

        if (string.IsNullOrWhiteSpace(result)) return;

        bool isValid = await _settingsService.VerifyTeacherTokenAsync(result);
        if (isValid)
        {
            _settingsService.IsTeacherModeActive = true;
            IsTeacherMode = true;
            await DisplayAlertAsync("Успешно", "Скрытый режим редактирования активирован через SecureStorage!");
        }
        else
        {
            await DisplayAlertAsync("Ошибка доступа", "Неверный токен преподавателя.");
        }
    }

    private async Task ExecuteCreateNewTestAsync()
    {
        await Shell.Current.GoToAsync("TeacherEditPage");
    }
}
