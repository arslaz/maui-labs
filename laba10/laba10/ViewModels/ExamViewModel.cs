using System.Collections.ObjectModel;
using System.Windows.Input;
using laba10.Models;
using laba10.Services;

namespace laba10.ViewModels;

/// <summary>
/// ViewModel страницы прохождения экзамена.
/// Реализует таймер обратного отсчета, динамическую смену вопросов через DataTemplateSelector,
/// симуляцию аудио-плеера, логику сопоставления пар со штрафами, проверку объема эссе,
/// расчет итогового балла по формуле и команду заморозки теста в JSON.
/// </summary>
public class ExamViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ITestService _testService;
    private readonly IResultService _resultService;
    private readonly ISettingsService _settingsService;

    private IDispatcherTimer? _examTimer;
    private IDispatcherTimer? _audioTimer;

    private ExamTest? _currentTest;
    private ObservableCollection<QuestionBase> _questions = new();
    private QuestionBase? _currentQuestion;
    private int _currentQuestionIndex = 0;
    private int _remainingSeconds;
    private string _formattedTimeRemaining = "00:00";
    private double _examProgress = 0.0;

    // Свойства для секции аудирования
    private bool _isAudioPlaying = false;
    private double _audioProgress = 0.0;
    private string _audioStatusText = "Нажмите Play для прослушивания диалога";

    // Свойства для секции Match Pairs
    private ObservableCollection<MatchPairDisplayItem> _matchPairDisplayItems = new();
    private ObservableCollection<string> _availableRightItems = new();
    private string? _selectedLeftItem;
    private string? _selectedRightItem;

    // Свойства для секции Эссе
    private string _essayText = string.Empty;
    private int _currentWordCount = 0;
    private bool _isWordCountMet = false;
    private string _wordCountStatusText = string.Empty;

    public ExamTest? CurrentTest
    {
        get => _currentTest;
        set => SetProperty(ref _currentTest, value);
    }

    public ObservableCollection<QuestionBase> Questions
    {
        get => _questions;
        set => SetProperty(ref _questions, value);
    }

    private ObservableCollection<QuestionBase> _currentQuestionList = new();
    public ObservableCollection<QuestionBase> CurrentQuestionList
    {
        get => _currentQuestionList;
        set => SetProperty(ref _currentQuestionList, value);
    }

    public QuestionBase? CurrentQuestion
    {
        get => _currentQuestion;
        set
        {
            if (SetProperty(ref _currentQuestion, value))
            {
                OnQuestionChanged();
            }
        }
    }

    public int CurrentQuestionIndex
    {
        get => _currentQuestionIndex;
        set => SetProperty(ref _currentQuestionIndex, value);
    }

    public int TotalQuestionsCount => Questions.Count;

    public string FormattedTimeRemaining
    {
        get => _formattedTimeRemaining;
        set => SetProperty(ref _formattedTimeRemaining, value);
    }

    public double ExamProgress
    {
        get => _examProgress;
        set => SetProperty(ref _examProgress, value);
    }

    // Аудио
    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        set => SetProperty(ref _isAudioPlaying, value);
    }

    public double AudioProgress
    {
        get => _audioProgress;
        set => SetProperty(ref _audioProgress, value);
    }

    public string AudioStatusText
    {
        get => _audioStatusText;
        set => SetProperty(ref _audioStatusText, value);
    }

    // Match Pairs
    public ObservableCollection<MatchPairDisplayItem> MatchPairDisplayItems
    {
        get => _matchPairDisplayItems;
        set => SetProperty(ref _matchPairDisplayItems, value);
    }

    public ObservableCollection<string> AvailableRightItems
    {
        get => _availableRightItems;
        set => SetProperty(ref _availableRightItems, value);
    }

    public string? SelectedLeftItem
    {
        get => _selectedLeftItem;
        set => SetProperty(ref _selectedLeftItem, value);
    }

    public string? SelectedRightItem
    {
        get => _selectedRightItem;
        set
        {
            if (SetProperty(ref _selectedRightItem, value) && value != null && SelectedLeftItem != null)
            {
                ConnectPair(SelectedLeftItem, value);
            }
        }
    }

    // Эссе
    public string EssayText
    {
        get => _essayText;
        set
        {
            if (SetProperty(ref _essayText, value))
            {
                UpdateEssayStats();
            }
        }
    }

    public int CurrentWordCount
    {
        get => _currentWordCount;
        set => SetProperty(ref _currentWordCount, value);
    }

    public bool IsWordCountMet
    {
        get => _isWordCountMet;
        set => SetProperty(ref _isWordCountMet, value);
    }

    public string WordCountStatusText
    {
        get => _wordCountStatusText;
        set => SetProperty(ref _wordCountStatusText, value);
    }

    public bool CanGoNext => CurrentQuestionIndex < TotalQuestionsCount - 1;
    public bool CanGoPrevious => CurrentQuestionIndex > 0;
    public bool IsLastQuestion => CurrentQuestionIndex == TotalQuestionsCount - 1;

    // Команды
    public ICommand NextQuestionCommand { get; }
    public ICommand PreviousQuestionCommand { get; }
    public ICommand GoToQuestionCommand { get; }
    public ICommand SelectListeningOptionCommand { get; }
    public ICommand PlayAudioCommand { get; }
    public ICommand PauseAudioCommand { get; }
    public ICommand SelectLeftItemCommand { get; }
    public ICommand ClearPairCommand { get; }
    public ICommand FreezeAndExitCommand { get; }
    public ICommand SubmitExamCommand { get; }

    public ExamViewModel(ITestService testService, IResultService resultService, ISettingsService settingsService)
    {
        _testService = testService;
        _resultService = resultService;
        _settingsService = settingsService;

        NextQuestionCommand = new Command(ExecuteNextQuestion);
        PreviousQuestionCommand = new Command(ExecutePreviousQuestion);
        GoToQuestionCommand = new Command<int>(ExecuteGoToQuestion);
        SelectListeningOptionCommand = new Command<int>(ExecuteSelectListeningOption);
        PlayAudioCommand = new Command(ExecutePlayAudio);
        PauseAudioCommand = new Command(ExecutePauseAudio);
        SelectLeftItemCommand = new Command<string>(ExecuteSelectLeftItem);
        ClearPairCommand = new Command<string>(ExecuteClearPair);
        FreezeAndExitCommand = new Command(async () => await ExecuteFreezeAndExitAsync());
        SubmitExamCommand = new Command(async () => await ExecuteSubmitExamAsync());
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TestId", out var testIdObj) && testIdObj is string testId)
        {
            bool isResuming = query.TryGetValue("IsResuming", out var resumeObj) && (bool)resumeObj;
            await SetupTestAsync(testId, isResuming);
        }
    }

    public async Task SetupTestAsync(string testId, bool isResuming)
    {
        if (isResuming)
        {
            var frozen = await _testService.GetFrozenStateAsync();
            if (frozen != null && frozen.TestId == testId)
            {
                var test = await _testService.GetTestByIdAsync(testId);
                CurrentTest = test;
                Questions = new ObservableCollection<QuestionBase>(frozen.Questions);
                _remainingSeconds = frozen.RemainingSeconds > 0 ? frozen.RemainingSeconds : 15 * 60;
                CurrentQuestionIndex = Math.Clamp(frozen.CurrentQuestionIndex, 0, Questions.Count - 1);
                CurrentQuestion = Questions[CurrentQuestionIndex];
                StartExamTimer();
                UpdateProgress();
                return;
            }
        }

        var fullTest = await _testService.GetTestByIdAsync(testId);
        if (fullTest != null)
        {
            CurrentTest = fullTest;
            // Делаем копию вопросов для изолированной сессии
            var clone = fullTest.Clone();
            Questions = new ObservableCollection<QuestionBase>(clone.Questions);
            _remainingSeconds = fullTest.TimeLimitMinutes * 60;
            CurrentQuestionIndex = 0;
            if (Questions.Count > 0)
            {
                CurrentQuestion = Questions[0];
            }
            StartExamTimer();
            UpdateProgress();
        }
    }

    private void StartExamTimer()
    {
        _examTimer?.Stop();
        _examTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_examTimer != null)
        {
            _examTimer.Interval = TimeSpan.FromSeconds(1);
            _examTimer.Tick += OnExamTimerTick;
            _examTimer.Start();
        }
        UpdateTimeDisplay();
    }

    private async void OnExamTimerTick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateTimeDisplay();
        }
        else
        {
            _examTimer?.Stop();
            await DisplayAlertAsync("Время истекло", "Экзаменационное время завершилось. Ваши ответы будут автоматически сохранены и проверены.");
            await ExecuteSubmitExamAsync();
        }
    }

    private void UpdateTimeDisplay()
    {
        int minutes = _remainingSeconds / 60;
        int seconds = _remainingSeconds % 60;
        FormattedTimeRemaining = $"{minutes:D2}:{seconds:D2}";
    }

    private void OnQuestionChanged()
    {
        if (CurrentQuestion == null) return;

        // Останавливаем аудио, если переходим между вопросами
        ExecutePauseAudio();

        if (CurrentQuestion is MatchPairsQuestion matchQ)
        {
            matchQ.RefreshDisplayItems();
        }
        else if (CurrentQuestion is EssayQuestion essayQ)
        {
            _essayText = essayQ.UserEssayText;
            OnPropertyChanged(nameof(EssayText));
            UpdateEssayStats();
        }

        CurrentQuestionList.Clear();
        CurrentQuestionList.Add(CurrentQuestion);

        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(IsLastQuestion));
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (TotalQuestionsCount > 0)
        {
            ExamProgress = (double)(CurrentQuestionIndex + 1) / TotalQuestionsCount;
        }
        OnPropertyChanged(nameof(TotalQuestionsCount));
    }

    // Аудио симуляция
    private void ExecutePlayAudio()
    {
        if (IsAudioPlaying) return;
        IsAudioPlaying = true;
        AudioStatusText = "Идет воспроизведение записи...";

        _audioTimer?.Stop();
        _audioTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_audioTimer != null)
        {
            _audioTimer.Interval = TimeSpan.FromMilliseconds(500);
            _audioTimer.Tick += (s, e) =>
            {
                AudioProgress += 0.05;
                if (AudioProgress >= 1.0)
                {
                    AudioProgress = 1.0;
                    ExecutePauseAudio();
                    AudioStatusText = "Прослушивание завершено. Выберите ответ.";
                }
            };
            _audioTimer.Start();
        }
    }

    private void ExecutePauseAudio()
    {
        IsAudioPlaying = false;
        _audioTimer?.Stop();
        if (AudioProgress > 0 && AudioProgress < 1.0)
        {
            AudioStatusText = "Воспроизведение приостановлено";
        }
    }

    private void ExecuteSelectListeningOption(int index)
    {
        if (CurrentQuestion is ListeningQuestion listening)
        {
            listening.SelectedOptionIndex = index;
            // Принудительно уведомляем UI об изменении
            OnPropertyChanged(nameof(CurrentQuestion));
        }
    }

    // Секция Match Pairs
    private void SetupMatchPairsView(MatchPairsQuestion matchQ)
    {
        SelectedLeftItem = null;
        SelectedRightItem = null;

        var displayList = new ObservableCollection<MatchPairDisplayItem>();
        foreach (var left in matchQ.LeftItems)
        {
            string? matched = matchQ.UserMatches.TryGetValue(left, out var right) ? right : null;
            displayList.Add(new MatchPairDisplayItem
            {
                LeftText = left,
                SelectedRightText = matched
            });
        }
        MatchPairDisplayItems = displayList;

        // Доступные правые элементы (те, что еще не выбраны)
        var usedRight = matchQ.UserMatches.Values.ToHashSet();
        var available = matchQ.RightItems.Where(r => !usedRight.Contains(r)).ToList();
        AvailableRightItems = new ObservableCollection<string>(available);
    }

    private void ExecuteSelectLeftItem(string? item)
    {
        SelectedLeftItem = item;
    }

    private void ConnectPair(string leftItem, string rightItem)
    {
        if (CurrentQuestion is MatchPairsQuestion matchQ)
        {
            matchQ.UserMatches[leftItem] = rightItem;
            SetupMatchPairsView(matchQ);
        }
    }

    private void ExecuteClearPair(string? leftItem)
    {
        if (leftItem != null && CurrentQuestion is MatchPairsQuestion matchQ)
        {
            if (matchQ.UserMatches.Remove(leftItem))
            {
                SetupMatchPairsView(matchQ);
            }
        }
    }

    // Секция Эссе
    private void UpdateEssayStats()
    {
        if (CurrentQuestion is EssayQuestion essayQ)
        {
            essayQ.UserEssayText = EssayText;
            CurrentWordCount = essayQ.GetCurrentWordCount();
            IsWordCountMet = CurrentWordCount >= essayQ.MinWordCount;

            if (IsWordCountMet)
            {
                WordCountStatusText = $"Слов: {CurrentWordCount} из {essayQ.MinWordCount} (Требование выполнено ✓)";
            }
            else
            {
                int missing = essayQ.MinWordCount - CurrentWordCount;
                WordCountStatusText = $"Слов: {CurrentWordCount} из {essayQ.MinWordCount} (Необходимо еще {missing} слов)";
            }
        }
    }

    private void ExecuteNextQuestion()
    {
        if (CanGoNext)
        {
            CurrentQuestionIndex++;
            CurrentQuestion = Questions[CurrentQuestionIndex];
        }
    }

    private void ExecutePreviousQuestion()
    {
        if (CanGoPrevious)
        {
            CurrentQuestionIndex--;
            CurrentQuestion = Questions[CurrentQuestionIndex];
        }
    }

    private void ExecuteGoToQuestion(int index)
    {
        if (index >= 0 && index < Questions.Count)
        {
            CurrentQuestionIndex = index;
            CurrentQuestion = Questions[index];
        }
    }

    // Заморозка теста
    private async Task ExecuteFreezeAndExitAsync()
    {
        if (CurrentTest == null) return;

        bool confirm = await DisplayConfirmationAsync(
            "Заморозка теста",
            "Вы действительно хотите заморозить прохождение теста? Текущий вопрос, таймер и все введенные ответы будут сохранены в изолированное JSON-хранилище.",
            "Заморозить и выйти", "Продолжить тест");

        if (!confirm) return;

        _examTimer?.Stop();
        _audioTimer?.Stop();

        // Сохраняем текущее эссе если сейчас на нем
        if (CurrentQuestion is EssayQuestion eq)
        {
            eq.UserEssayText = EssayText;
        }

        var frozen = new FrozenTestState
        {
            TestId = CurrentTest.Id,
            TestTitle = CurrentTest.Title,
            Level = CurrentTest.Level,
            CurrentQuestionIndex = CurrentQuestionIndex,
            RemainingSeconds = _remainingSeconds,
            SavedAt = DateTime.Now,
            Questions = Questions.ToList()
        };

        await _testService.SaveFrozenStateAsync(frozen);

        await DisplayAlertAsync("Тест заморожен", 
            $"Состояние успешно зафиксировано в JSON-файле.\nВы сможете продолжить тестирование в любой момент с вопроса №{CurrentQuestionIndex + 1}.", 
            "В главное меню");

        await Shell.Current.GoToAsync("..");
    }

    // Завершение и расчет баллов по сложной формуле
    private async Task ExecuteSubmitExamAsync()
    {
        if (CurrentTest == null) return;

        _examTimer?.Stop();
        _audioTimer?.Stop();

        if (CurrentQuestion is EssayQuestion eq)
        {
            eq.UserEssayText = EssayText;
        }

        IsBusy = true;

        try
        {
            double listeningScore = 0.0;
            double matchPairsScore = 0.0;
            double essayScore = 0.0;
            int totalMatchErrors = 0;
            int essayWords = 0;

            // Расчет по каждому вопросу
            foreach (var q in Questions)
            {
                if (q is ListeningQuestion lq)
                {
                    if (lq.SelectedOptionIndex == lq.CorrectOptionIndex)
                    {
                        listeningScore = lq.MaxPoints;
                        lq.EarnedPoints = lq.MaxPoints;
                        lq.Feedback = "Верно: точный ответ на вопрос аудиозаписи.";
                    }
                    else
                    {
                        listeningScore = 0.0;
                        lq.EarnedPoints = 0.0;
                        lq.Feedback = $"Ошибка в аудировании. Выбран неверный вариант.";
                    }
                }
                else if (q is MatchPairsQuestion mq)
                {
                    int errors = 0;
                    foreach (var pair in mq.CorrectMatches)
                    {
                        if (!mq.UserMatches.TryGetValue(pair.Key, out var userRight) || userRight != pair.Value)
                        {
                            errors++;
                        }
                    }
                    totalMatchErrors = errors;

                    // Формула со штрафами за неверные пары:
                    // Баллы = Max(0, MaxPoints - (errors * PenaltyPerMistake))
                    double penalty = errors * mq.PenaltyPerMistake;
                    matchPairsScore = Math.Max(0.0, mq.MaxPoints - penalty);
                    mq.EarnedPoints = matchPairsScore;
                    mq.Feedback = errors == 0 
                        ? "Идеально: все соответствия установлены безошибочно!" 
                        : $"Ошибок в парах: {errors}. Наложен штраф: {penalty:F1} баллов.";
                }
                else if (q is EssayQuestion eqItem)
                {
                    essayWords = eqItem.GetCurrentWordCount();
                    if (essayWords < eqItem.MinWordCount)
                    {
                        // Жесткий штраф за недобор слов
                        double ratio = (double)essayWords / eqItem.MinWordCount;
                        essayScore = eqItem.MaxPoints * 0.3 * ratio;
                        eqItem.Feedback = $"Штраф: объем эссе ({essayWords} слов) меньше минимума ({eqItem.MinWordCount} слов).";
                    }
                    else
                    {
                        // Базовый балл за объем + проверка ключевых терминов
                        double baseScore = eqItem.MaxPoints * 0.8;
                        double keywordBonus = 0;
                        string lower = eqItem.UserEssayText.ToLowerInvariant();
                        foreach (var kw in eqItem.RequiredKeywords)
                        {
                            if (lower.Contains(kw.ToLowerInvariant()))
                                keywordBonus += 1.5;
                        }
                        essayScore = Math.Min(eqItem.MaxPoints, baseScore + keywordBonus);
                        eqItem.Feedback = $"Требование по объему выполнено ({essayWords} слов). Бонус за аргументацию начислен.";
                    }
                    eqItem.EarnedPoints = essayScore;
                }
            }

            double totalEarned = listeningScore + matchPairsScore + essayScore;
            double maxPoints = CurrentTest.GetMaxTotalPoints();
            double totalPercentage = Math.Min(100.0, (totalEarned / maxPoints) * 100.0);

            // Определение CEFR уровня по шкале
            string cefrGrade = totalPercentage switch
            {
                >= 85 => "C1 (Mastery)",
                >= 70 => "B2 (Vantage)",
                >= 55 => "B1 (Threshold)",
                >= 40 => "A2 (Waystage)",
                _ => "A1 (Breakthrough)"
            };

            // Создание и сохранение результата
            var result = new ExamResult
            {
                TestId = CurrentTest.Id,
                TestTitle = CurrentTest.Title,
                Level = CurrentTest.Level,
                Date = DateTime.Now,
                TotalScore = totalPercentage,
                CefrGrade = cefrGrade,
                ListeningScore = listeningScore,
                MatchPairsScore = matchPairsScore,
                EssayScore = essayScore,
                MatchErrorsCount = totalMatchErrors,
                EssayWordCount = essayWords,
                IsAppealed = false,
                AppealStatus = "Не подана"
            };

            await _resultService.SaveResultAsync(result);
            await _testService.UpdateTestResultAsync(CurrentTest.Id, totalPercentage);
            await _testService.ClearFrozenStateAsync();

            string summary = 
                $"Итоговый балл: {totalPercentage:F1}%\n" +
                $"Подтвержденный уровень: {cefrGrade}\n\n" +
                $"• Аудирование: {listeningScore:F1} б.\n" +
                $"• Пары соответствий: {matchPairsScore:F1} б. (Ошибок: {totalMatchErrors})\n" +
                $"• Эссе: {essayScore:F1} б. ({essayWords} слов)\n\n" +
                $"Результат подписан крипто-хэшем в SecureStorage.";

            await DisplayAlertAsync("Экзамен завершен!", summary, "Посмотреть статистику");

            // Переход на страницу статистики и результатов
            await Shell.Current.GoToAsync("//SettingsAndStatsPage");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Ошибка проверки", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
