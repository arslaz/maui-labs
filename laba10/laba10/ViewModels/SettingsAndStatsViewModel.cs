using System.Collections.ObjectModel;
using System.Windows.Input;
using laba10.Models;
using laba10.Services;

namespace laba10.ViewModels;

/// <summary>
/// ViewModel для настроек (Preferences), безопасного режима преподавателя (SecureStorage)
/// и глобальной статистики со списком результатов и контекстным меню апелляции, экспорта и удаления.
/// </summary>
public class SettingsAndStatsViewModel : BaseViewModel
{
    private readonly IResultService _resultService;
    private readonly ISettingsService _settingsService;

    private string _studentName = string.Empty;
    private string _targetLevel = "Все";
    private string _teacherTokenInput = string.Empty;
    private bool _isTeacherUnlocked = false;
    private string _teacherStatusText = "Требуется авторизация через SecureStorage";

    private ObservableCollection<ExamResult> _resultsHistory = new();

    // Статистика
    private int _totalExamsTaken = 0;
    private double _averageScore = 0.0;
    private double _bestScore = 0.0;
    private string _bestCefrLevel = "—";
    private int _totalAppealsCount = 0;

    public List<string> AvailableLevels { get; } = new() { "Все", "B1", "B2", "C1" };

    public string StudentName
    {
        get => _studentName;
        set
        {
            if (SetProperty(ref _studentName, value))
            {
                _settingsService.StudentName = value;
            }
        }
    }

    public string TargetLevel
    {
        get => _targetLevel;
        set
        {
            if (SetProperty(ref _targetLevel, value))
            {
                _settingsService.TargetLanguageLevel = value;
            }
        }
    }

    public string TeacherTokenInput
    {
        get => _teacherTokenInput;
        set => SetProperty(ref _teacherTokenInput, value);
    }

    public bool IsTeacherUnlocked
    {
        get => _isTeacherUnlocked;
        set => SetProperty(ref _isTeacherUnlocked, value);
    }

    public string TeacherStatusText
    {
        get => _teacherStatusText;
        set => SetProperty(ref _teacherStatusText, value);
    }

    public ObservableCollection<ExamResult> ResultsHistory
    {
        get => _resultsHistory;
        set => SetProperty(ref _resultsHistory, value);
    }

    // Метрики
    public int TotalExamsTaken
    {
        get => _totalExamsTaken;
        set => SetProperty(ref _totalExamsTaken, value);
    }

    public double AverageScore
    {
        get => _averageScore;
        set => SetProperty(ref _averageScore, value);
    }

    public double BestScore
    {
        get => _bestScore;
        set => SetProperty(ref _bestScore, value);
    }

    public string BestCefrLevel
    {
        get => _bestCefrLevel;
        set => SetProperty(ref _bestCefrLevel, value);
    }

    public int TotalAppealsCount
    {
        get => _totalAppealsCount;
        set => SetProperty(ref _totalAppealsCount, value);
    }

    // Команды
    public ICommand LoadDataCommand { get; }
    public ICommand VerifyTeacherTokenCommand { get; }
    public ICommand LockTeacherModeCommand { get; }
    public ICommand AppealResultCommand { get; }
    public ICommand ExportResultCommand { get; }
    public ICommand DeleteResultCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand ShowResultContextMenuCommand { get; }

    public SettingsAndStatsViewModel(IResultService resultService, ISettingsService settingsService)
    {
        _resultService = resultService;
        _settingsService = settingsService;
        Title = "Настройки и Статистика";

        LoadDataCommand = new Command(async () => await LoadDataAsync());
        VerifyTeacherTokenCommand = new Command(async () => await ExecuteVerifyTeacherTokenAsync());
        LockTeacherModeCommand = new Command(ExecuteLockTeacherMode);
        AppealResultCommand = new Command<ExamResult>(async (r) => await ExecuteAppealResultAsync(r));
        ExportResultCommand = new Command<ExamResult>(async (r) => await ExecuteExportResultAsync(r));
        DeleteResultCommand = new Command<ExamResult>(async (r) => await ExecuteDeleteResultAsync(r));
        ClearHistoryCommand = new Command(async () => await ExecuteClearHistoryAsync());
        ShowResultContextMenuCommand = new Command<ExamResult>(async (r) => await ExecuteShowResultContextMenuAsync(r));
    }

    private async Task ExecuteShowResultContextMenuAsync(ExamResult? result)
    {
        if (result == null) return;
        string action = await DisplayActionSheetAsync(
            $"Контекстное меню: {result.TestTitle}",
            "Отмена",
            "Удалить запись из истории",
            "Подать апелляцию (Генерация отчета об ошибках)",
            "Имитация экспорта для отправки результата");

        switch (action)
        {
            case "Подать апелляцию (Генерация отчета об ошибках)":
                await ExecuteAppealResultAsync(result);
                break;
            case "Имитация экспорта для отправки результата":
                await ExecuteExportResultAsync(result);
                break;
            case "Удалить запись из истории":
                await ExecuteDeleteResultAsync(result);
                break;
        }
    }

    public async Task InitializeAsync()
    {
        _studentName = _settingsService.StudentName;
        _targetLevel = _settingsService.TargetLanguageLevel;
        _isTeacherUnlocked = _settingsService.IsTeacherModeActive;
        UpdateTeacherStatusDisplay();
        OnPropertyChanged(nameof(StudentName));
        OnPropertyChanged(nameof(TargetLevel));
        OnPropertyChanged(nameof(IsTeacherUnlocked));

        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var results = await _resultService.GetResultsAsync();
            ResultsHistory = new ObservableCollection<ExamResult>(results);
            CalculateStatistics(results);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", $"Не удалось загрузить историю: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CalculateStatistics(List<ExamResult> results)
    {
        TotalExamsTaken = results.Count;
        if (results.Count > 0)
        {
            AverageScore = results.Average(r => r.TotalScore);
            BestScore = results.Max(r => r.TotalScore);
            var bestResult = results.OrderByDescending(r => r.TotalScore).First();
            BestCefrLevel = bestResult.CefrGrade;
            TotalAppealsCount = results.Count(r => r.IsAppealed);
        }
        else
        {
            AverageScore = 0;
            BestScore = 0;
            BestCefrLevel = "—";
            TotalAppealsCount = 0;
        }
    }

    private async Task ExecuteVerifyTeacherTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(TeacherTokenInput))
        {
            await DisplayAlertAsync("Внимание", "Введите токен преподавателя (например: TEACHER2026).");
            return;
        }

        bool isValid = await _settingsService.VerifyTeacherTokenAsync(TeacherTokenInput);
        if (isValid)
        {
            _settingsService.IsTeacherModeActive = true;
            IsTeacherUnlocked = true;
            TeacherTokenInput = string.Empty;
            UpdateTeacherStatusDisplay();
            await DisplayAlertAsync("Доступ разрешен", 
                "Ключ успешно верифицирован в SecureStorage!\nРежим скрытого редактирования и добавления вопросов активен.");
        }
        else
        {
            await DisplayAlertAsync("Отказ в доступе", "Введен неверный токен преподавателя.");
        }
    }

    private void ExecuteLockTeacherMode()
    {
        _settingsService.IsTeacherModeActive = false;
        IsTeacherUnlocked = false;
        UpdateTeacherStatusDisplay();
    }

    private void UpdateTeacherStatusDisplay()
    {
        TeacherStatusText = IsTeacherUnlocked 
            ? "Режим преподавателя активен (SecureStorage токен подтвержден)" 
            : "Режим преподавателя заблокирован";
        OnPropertyChanged(nameof(TeacherStatusText));
    }

    // Контекстное меню 1: Подача апелляции с генерацией текстового отчета об ошибках
    private async Task ExecuteAppealResultAsync(ExamResult? result)
    {
        if (result == null) return;

        if (result.IsAppealed)
        {
            await DisplayAlertAsync("Апелляция уже подана", 
                $"Ранее сгенерированный отчет:\n\n{result.DetailedReport}");
            return;
        }

        bool confirm = await DisplayConfirmationAsync(
            "Подача апелляции", 
            $"Сформировать официальный отчет об ошибках и направить апелляцию по экзамену «{result.TestTitle}»?",
            "Подать апелляцию", "Отмена");

        if (!confirm) return;

        try
        {
            string reportText = await _resultService.ProcessAppealAsync(result.Id);
            await LoadDataAsync();

            await DisplayAlertAsync("Апелляция зарегистрирована", 
                $"Сформирован официальный протокол пересмотра:\n\n{reportText}");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка апелляции", ex.Message);
        }
    }

    // Контекстное меню 2: Имитация экспорта для отправки результата
    private async Task ExecuteExportResultAsync(ExamResult? result)
    {
        if (result == null) return;

        try
        {
            string exportPath = await _resultService.ExportResultCertificateAsync(result.Id);
            await DisplayAlertAsync("Экспорт сертификата", 
                $"Сертификат владения языком успешно экспортирован:\n{exportPath}\n\nКриптографическая подпись SecureStorage верифицирована. Файл готов к отправке в приемную комиссию.");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка экспорта", ex.Message);
        }
    }

    // Контекстное меню 3: Удаление записи из истории
    private async Task ExecuteDeleteResultAsync(ExamResult? result)
    {
        if (result == null) return;

        bool confirm = await DisplayConfirmationAsync("Удаление результата", 
            $"Удалить запись об экзамене «{result.TestTitle}» от {result.Date:dd.MM.yyyy}?");
        if (!confirm) return;

        try
        {
            bool deleted = await _resultService.DeleteResultAsync(result.Id);
            if (deleted)
            {
                ResultsHistory.Remove(result);
                CalculateStatistics(ResultsHistory.ToList());
                await DisplayAlertAsync("Успешно", "Запись удалена из истории.");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка", ex.Message);
        }
    }

    private async Task ExecuteClearHistoryAsync()
    {
        bool confirm = await DisplayConfirmationAsync("Очистка всей истории", 
            "Вы действительно хотите удалить все результаты экзаменов?", "Очистить", "Отмена");
        if (!confirm) return;

        await _resultService.ClearAllResultsAsync();
        ResultsHistory.Clear();
        CalculateStatistics(new List<ExamResult>());
        await DisplayAlertAsync("Очищено", "История экзаменов полностью очищена.");
    }
}
